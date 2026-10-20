// Runs conversion jobs in the background: plans routes, limits engine concurrency,
// allocates output paths and reports progress. UI code only enqueues and listens to events.

using System.Collections.Concurrent;
using System.Threading.Channels;
using DropMorph.Core.Formats;
using DropMorph.Core.Presets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DropMorph.Core.Conversion;

/// <summary>Background queue that executes <see cref="ConversionJob"/>s one after another.</summary>
public sealed class JobQueue : IAsyncDisposable
{
    private readonly ConverterCatalog _catalog;
    private readonly IPdfMerger? _pdfMerger;
    private readonly IFileCombiner? _combiner;
    private readonly ITiffMerger? _tiffMerger;
    private readonly ILogger _log;
    private readonly Channel<ConversionJob> _channel = Channel.CreateUnbounded<ConversionJob>();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _engineGates = new();
    private readonly HashSet<string> _reservedOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task _worker;
    private int _busy;

    /// <param name="catalog">Converters and their priority.</param>
    /// <param name="pdfMerger">Used by "Merge into one PDF" presets.</param>
    /// <param name="log">Optional logger.</param>
    /// <param name="combiner">Used by "Compress into one archive" presets (<see cref="ArchiveOptions.CombineIntoOne"/>).</param>
    /// <param name="tiffMerger">Used by multi-page TIFF presets (<see cref="ImageOptions.MultiPageTiff"/>).</param>
    public JobQueue(ConverterCatalog catalog, IPdfMerger? pdfMerger = null, ILogger<JobQueue>? log = null, IFileCombiner? combiner = null,
        ITiffMerger? tiffMerger = null)
    {
        _catalog = catalog;
        _pdfMerger = pdfMerger;
        _combiner = combiner;
        _tiffMerger = tiffMerger;
        _log = log ?? (ILogger)NullLogger.Instance;
        _worker = Task.Run(WorkLoopAsync);
    }

    /// <summary>Raised (on a background thread) whenever a job's state or progress changes.</summary>
    public event EventHandler<ConversionJob>? JobUpdated;

    /// <summary>Raised (on a background thread) once when a job reaches a final state.</summary>
    public event EventHandler<ConversionJob>? JobFinished;

    /// <summary>True while a job waits in the queue or runs (the app frees memory only when this is false).</summary>
    public bool IsBusy => Volatile.Read(ref _busy) > 0;

    /// <summary>Adds a job to the queue and returns it immediately.</summary>
    public ConversionJob Enqueue(IReadOnlyList<string> sources, Preset preset, string presetDisplayName)
    {
        var job = new ConversionJob(sources, preset, presetDisplayName);
        Interlocked.Increment(ref _busy); // until the worker is done with it
        _channel.Writer.TryWrite(job);
        JobUpdated?.Invoke(this, job);
        return job;
    }

    /// <summary>Runs a job to completion on the calling task. Used by tests and by <see cref="Enqueue"/>'s worker.</summary>
    public async Task RunAsync(ConversionJob job)
    {
        Interlocked.Increment(ref _busy);
        try
        {
            await RunJobAsync(job);
        }
        finally
        {
            Interlocked.Decrement(ref _busy);
        }
    }

    private async Task RunJobAsync(ConversionJob job)
    {
        job.State = JobState.Running;
        Notify(job);

        var workDir = Path.Combine(Path.GetTempPath(), "DropMorph", job.Id);
        Directory.CreateDirectory(workDir);
        try
        {
            var planner = _catalog.CreatePlanner();
            var merge = job.Preset.Pdf.MergeIntoSingle
                        && job.Preset.TargetFormat == "pdf"
                        && job.Files.Count > 1
                        && _pdfMerger is not null;
            // Any files, one of them or many, archives or not: they go into the archive as they are.
            var combine = job.Preset.PacksIntoOneArchive && _combiner is not null;

            // One file is enough: a 10-page PDF becomes one 10-page TIFF.
            var multiPageTiff = job.Preset.Image.MultiPageTiff && job.Preset.TargetFormat == "tiff" && _tiffMerger is not null;

            if (merge)
                await RunMergedAsync(job, planner, workDir);
            else if (multiPageTiff)
                await RunMultiPageTiffAsync(job, planner, workDir);
            else if (combine)
                await RunCombinedAsync(job, workDir);
            else
                await Parallel.ForEachAsync(
                    job.Files.Select((f, i) => (File: f, Index: i + 1)),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = CancellationToken.None },
                    async (item, _) => await RunFileAsync(job, item.File, item.Index, planner, workDir, finalOutput: true));

            job.State = job.CancellationToken.IsCancellationRequested ? JobState.Cancelled
                : job.Files.All(f => f.State is FileState.Done or FileState.Skipped) ? JobState.Completed
                : job.Files.Any(f => f.State == FileState.Done) ? JobState.CompletedWithErrors
                : JobState.Failed;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Job {Job} failed", job.Id);
            job.State = JobState.Failed;
        }
        finally
        {
            lock (_reservedOutputs)
                foreach (var output in job.Outputs)
                    _reservedOutputs.Remove(output);
            TryDelete(workDir);
        }

        Notify(job);
        JobFinished?.Invoke(this, job);
    }

    /// <summary>Converts every file to PDF in the work directory, then merges them into one output.</summary>
    private async Task RunMergedAsync(ConversionJob job, RoutePlanner planner, string workDir)
    {
        var parts = new string?[job.Files.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, job.Files.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (i, _) =>
            {
                var file = job.Files[i];
                // An empty PDF goes the long way, where it fails as empty instead of breaking the whole merge.
                if (FormatRegistry.Detect(file.SourcePath)?.Id == "pdf" && !IsEmptyFile(file.SourcePath))
                {
                    // Already a PDF, merged as it is: its pages can't be turned (only images going into it can).
                    if (job.Preset.Image.TurnsOrMirrors)
                        FailUnturned(job, file, "pdf → pdf");
                    else
                        parts[i] = file.SourcePath;
                    return;
                }
                var produced = await RunFileAsync(job, file, i + 1, planner, workDir, finalOutput: false);
                parts[i] = produced.FirstOrDefault();
            });

        var inputs = parts.OfType<string>().ToList();
        if (inputs.Count == 0 || job.CancellationToken.IsCancellationRequested)
            return;

        var first = job.Files[0];
        var allocator = CreateFinalAllocator(job, first.SourcePath, 1);
        var output = allocator.Allocate("pdf");
        if (output is null)
        {
            foreach (var f in job.Files) f.State = FileState.Skipped;
            return;
        }

        await _pdfMerger!.MergeAsync(inputs, output, job.CancellationToken);
        first.Outputs.Add(output);
        foreach (var f in job.Files.Where(f => f.State != FileState.Failed))
        {
            f.State = FileState.Done;
            f.Progress = 1;
        }
        Notify(job);
    }

    /// <summary>
    /// Converts every file to TIFF pages in the work directory (a TIFF is taken as it is), then writes them all as one
    /// multi-page TIFF named after the first file.
    /// </summary>
    private async Task RunMultiPageTiffAsync(ConversionJob job, RoutePlanner planner, string workDir)
    {
        var parts = new IReadOnlyList<string>[job.Files.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, job.Files.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (i, _) =>
            {
                var file = job.Files[i];
                // A TIFF joins as it is, unless it is to be turned: then it goes through the engine like the rest.
                parts[i] = FormatRegistry.Detect(file.SourcePath)?.Id == "tiff" && !job.Preset.Image.TurnsOrMirrors
                           && !IsEmptyFile(file.SourcePath)
                    ? [file.SourcePath]
                    : await RunFileAsync(job, file, i + 1, planner, workDir, finalOutput: false, intermediate: "tiff");
            });

        var inputs = parts.SelectMany(p => p ?? []).ToList();
        if (inputs.Count == 0 || job.CancellationToken.IsCancellationRequested)
            return;

        var first = job.Files[0];
        var output = CreateFinalAllocator(job, first.SourcePath, 1).Allocate("tiff");
        if (output is null)
        {
            foreach (var f in job.Files) f.State = FileState.Skipped;
            return;
        }

        await _tiffMerger!.MergeAsync(inputs, output, job.Preset.Image, job.CancellationToken);
        first.Outputs.Add(output);
        foreach (var f in job.Files.Where(f => f.State != FileState.Failed))
        {
            f.State = FileState.Done;
            f.Progress = 1;
        }
        Notify(job);
    }

    /// <summary>A file that would be used as it is although the preset turns or mirrors it.</summary>
    private void FailUnturned(ConversionJob job, FileResult file, string detail)
    {
        file.State = FileState.Failed;
        file.ErrorKey = "error.cannot_turn";
        file.ErrorDetail = detail;
        file.Progress = 1;
        Notify(job);
    }

    /// <summary>Packs every source file, unchanged, into one archive named after the first file.</summary>
    private async Task RunCombinedAsync(ConversionJob job, string workDir)
    {
        var format = FormatRegistry.Get(job.Preset.TargetFormat);
        var inputs = new List<FileResult>();
        foreach (var file in job.Files)
        {
            // Folders are packed with their content.
            if (File.Exists(file.SourcePath) || Directory.Exists(file.SourcePath))
                inputs.Add(file);
            else
                MarkFailed(file, "The file no longer exists.");
        }
        if (inputs.Count == 0)
        {
            Notify(job);
            return;
        }

        var output = CreateFinalAllocator(job, inputs[0].SourcePath, 1).Allocate(format.PrimaryExtension);
        if (output is null)
        {
            foreach (var f in inputs)
            {
                f.State = FileState.Skipped;
                f.Progress = 1;
            }
            Notify(job);
            return;
        }

        foreach (var f in inputs)
            f.State = FileState.Running;
        Notify(job);
        var progress = new Progress<double>(p =>
        {
            // Reports arrive on the thread pool, possibly out of order: progress only moves forward.
            var value = Math.Clamp(p, 0, 1);
            if (value <= inputs[0].Progress)
                return;
            foreach (var f in inputs)
                f.Progress = value;
            Notify(job);
        });

        try
        {
            await _combiner!.CombineAsync(inputs.Select(f => f.SourcePath).ToList(), output, format.Id, job.Preset,
                workDir, progress, job.CancellationToken);
            inputs[0].Outputs.Add(output);
            foreach (var f in inputs)
            {
                f.State = FileState.Done;
                f.Progress = 1;
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var f in inputs)
                f.State = FileState.Cancelled;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Packing {Count} file(s) into {Output} failed", inputs.Count, output);
            foreach (var f in inputs)
                MarkFailed(f, ex.Message);
        }
        Notify(job);

        static void MarkFailed(FileResult f, string detail)
        {
            f.State = FileState.Failed;
            f.ErrorKey = "error.conversion_failed";
            f.ErrorDetail = detail;
            f.Progress = 1;
        }
    }

    /// <summary>A file (not a folder) with nothing in it. Combining still packs such a file as it is.</summary>
    private static bool IsEmptyFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Gives the outputs the creation and modification dates of the source (<see cref="OutputRule.KeepDates"/>).</summary>
    private void CopyDates(string source, IEnumerable<string> outputs)
    {
        try
        {
            var created = File.GetCreationTimeUtc(source);
            var modified = File.GetLastWriteTimeUtc(source);
            foreach (var output in outputs)
            {
                if (File.Exists(output))
                {
                    File.SetCreationTimeUtc(output, created);
                    File.SetLastWriteTimeUtc(output, modified);
                }
                else if (Directory.Exists(output))
                {
                    Directory.SetCreationTimeUtc(output, created);
                    Directory.SetLastWriteTimeUtc(output, modified);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The conversion itself worked: keep the files, only the dates are new.
            _log.LogWarning(ex, "Could not copy the dates of {Source}", source);
        }
    }

    /// <summary>
    /// Converts one source file along its planned route.
    /// With <paramref name="finalOutput"/> false, results stay in the work directory as <paramref name="intermediate"/>
    /// files (PDF for merging, TIFF pages for a multi-page TIFF).
    /// </summary>
    private async Task<IReadOnlyList<string>> RunFileAsync(
        ConversionJob job, FileResult file, int index, RoutePlanner planner, string workDir, bool finalOutput,
        string intermediate = "pdf")
    {
        var ct = job.CancellationToken;
        var source = FormatRegistry.Detect(file.SourcePath);
        if (source is null)
            return Fail(file, "error.unsupported_source", "." + FormatRegistry.ExtensionOf(file.SourcePath));
        // A file just made with "New" in the file manager stays 0 bytes until its app saves it: no engine can read it,
        // and their errors ("Central Directory corrupt") don't say why.
        if (IsEmptyFile(file.SourcePath))
            return Fail(file, "error.empty_file", null);

        var target = !finalOutput ? intermediate
            : job.Preset.TargetFormat == BuiltInData.SameAsSource ? source.Id
            : job.Preset.TargetFormat;

        // "Save every frame": a video becomes a folder of pictures in one step of its own. Never for an intermediate
        // file, and never on the way to another format (video → JPG is otherwise one still frame).
        var frames = finalOutput && job.Preset.SavesFramesOf(source.Id);
        var route = planner.Plan(source.Id, target, frames);
        if (route is null)
            return Fail(file, "error.no_route", $"{source.Id} → {target}");

        // A step that draws text without shaping (rhwp) would break Arabic, Hebrew, Indic or Thai text: such a file
        // takes a route without it (LibreOffice), or is refused with the reason rather than written broken.
        if (route.Steps.FirstOrDefault(s => s.Unshaped)?.Converter is ITextShapingCheck check
            && ScriptNeedingShaping(check, file.SourcePath, source.Id) is { } script)
        {
            _log.LogInformation("{File} has {Script} text, which {Engine} can't draw: planning a route without it",
                file.SourcePath, script, route.Steps.First(s => s.Unshaped).Converter.DisplayName);
            route = planner.Plan(source.Id, target, frames, shaping: true);
            if (route is null)
                return Fail(file, "error.needs_shaping", script);
        }

        // Rotate / flip: only an engine that turns images does it. Without one the file would come out unturned
        // (a PDF or a video re-encoded "as rotated"); with two in the route (EPS → PNG → JPG) it would turn twice.
        var turnAt = job.Preset.Image.TurnsOrMirrors ? route.FirstTransformStep() : -1;
        if (job.Preset.Image.TurnsOrMirrors && turnAt < 0)
            return Fail(file, "error.cannot_turn", $"{source.Id} → {target}");
        var unturned = turnAt < 0 ? job.Preset : Unturned(job.Preset);

        file.State = FileState.Running;
        Notify(job);

        try
        {
            IReadOnlyList<string> inputs = [file.SourcePath];
            // "Save as ZIP": the last step writes into the work folder and the files are packed into one ZIP afterwards.
            var zipResult = finalOutput && job.Preset.Output.ZipResult;
            for (var s = 0; s < route.Steps.Count; s++)
            {
                var step = route.Steps[s];
                var isLast = s == route.Steps.Count - 1;
                IOutputAllocator allocator = isLast && finalOutput && !zipResult
                    ? CreateFinalAllocator(job, file.SourcePath, index)
                    : new TempAllocator(workDir, FormatRegistry.NameWithoutExtension(file.SourcePath));

                var outputs = new List<string>();
                foreach (var input in inputs)
                {
                    ct.ThrowIfCancellationRequested();
                    var stepIndex = s;
                    var progress = new Progress<double>(p =>
                    {
                        // Reports arrive on the thread pool, possibly out of order: progress only moves forward.
                        var value = (stepIndex + Math.Clamp(p, 0, 1)) / route.Steps.Count;
                        if (value <= file.Progress)
                            return;
                        file.Progress = value;
                        Notify(job);
                    });

                    var gate = GateFor(step.Converter);
                    await gate.WaitAsync(ct);
                    try
                    {
                        // Turned once, in step turnAt: every other step gets the preset without rotation and flip,
                        // also one before it whose engine applies image options but doesn't say it turns.
                        var preset = s == turnAt ? job.Preset : unturned;
                        // Playback speed changes once, in the step that writes the output: an animation passed from one
                        // engine to another would otherwise change speed in both.
                        if (preset.Image.ChangesSpeed && !(isLast && finalOutput))
                            preset = AtOriginalSpeed(preset);
                        var stepInfo = new ConversionStep(input, step.From, step.To, preset, allocator, workDir, step.FrameSequence,
                            key => file.NoticeKey = key);
                        outputs.AddRange(await step.Converter.ConvertAsync(stepInfo, progress, ct));
                    }
                    finally
                    {
                        gate.Release();
                    }
                }
                inputs = outputs;
            }

            if (zipResult && inputs.Count > 0)
                inputs = PackZip(job, file, index, inputs, ct);

            if (finalOutput)
            {
                file.Outputs.AddRange(inputs);
                if (job.Preset.Output.KeepDates)
                    CopyDates(file.SourcePath, inputs);
            }
            file.State = inputs.Count == 0 && finalOutput ? FileState.Skipped : FileState.Done;
            file.Progress = 1;
            Notify(job);
            return inputs;
        }
        catch (OperationCanceledException)
        {
            file.State = FileState.Cancelled;
            Notify(job);
            return [];
        }
        catch (ConversionRefusedException ex)
        {
            _log.LogInformation("Converting {File} was refused: {Reason}", file.SourcePath, ex.Message);
            return Fail(file, ex.ErrorKey, ex.Detail);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Converting {File} failed", file.SourcePath);
            return Fail(file, "error.conversion_failed", ex.Message);
        }

        static Preset Unturned(Preset preset)
        {
            var copy = preset.Clone();
            copy.Image.Rotation = ImageRotation.None;
            copy.Image.Flip = ImageFlip.None;
            return copy;
        }

        static Preset AtOriginalSpeed(Preset preset)
        {
            var copy = preset.Clone();
            copy.Image.AnimationSpeed = 1;
            return copy;
        }

        IReadOnlyList<string> Fail(FileResult f, string key, string? detail)
        {
            f.State = FileState.Failed;
            f.ErrorKey = key;
            f.ErrorDetail = detail;
            f.Progress = 1;
            Notify(job);
            return [];
        }
    }

    /// <summary>
    /// Packs the files one source was converted to into one ZIP (named by the preset's output rule, so the conflict
    /// policy applies to it) and returns it; empty when the rule says to skip an existing ZIP. Files keep their names
    /// inside, a clash gets "name (2).ext", and a ZIP that can't be completed is deleted.
    /// </summary>
    private IReadOnlyList<string> PackZip(ConversionJob job, FileResult file, int index, IReadOnlyList<string> files, CancellationToken ct)
    {
        if (CreateFinalAllocator(job, file.SourcePath, index).Allocate("zip") is not { } zipPath)
            return [];
        try
        {
            using (var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(path);
                    for (var n = 2; !used.Add(name); n++)
                        name = $"{Path.GetFileNameWithoutExtension(path)} ({n}){Path.GetExtension(path)}";
                    System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(archive, path, name, System.IO.Compression.CompressionLevel.Optimal);
                }
            }
        }
        catch
        {
            try
            {
                File.Delete(zipPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
        return [zipPath];
    }

    /// <summary>The engine's check; a check that fails (an unreadable file) finds nothing, and the conversion reports the file.</summary>
    private string? ScriptNeedingShaping(ITextShapingCheck check, string path, string format)
    {
        try
        {
            return check.ScriptNeedingShaping(path, format);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not check the scripts of {File}", path);
            return null;
        }
    }

    private SemaphoreSlim GateFor(IConverter converter) =>
        _engineGates.GetOrAdd(converter.Id, _ =>
        {
            var max = converter.MaxParallelism <= 0 ? Environment.ProcessorCount : converter.MaxParallelism;
            return new SemaphoreSlim(max, max);
        });

    private FinalAllocator CreateFinalAllocator(ConversionJob job, string sourcePath, int index) =>
        new(this, job.Preset.Output, new OutputPathResolver.Tokens(sourcePath, job.PresetDisplayName, index, job.CreatedAt));

    private void Notify(ConversionJob job)
    {
        try
        {
            JobUpdated?.Invoke(this, job);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "JobUpdated handler threw");
        }
    }

    private async Task WorkLoopAsync()
    {
        await foreach (var job in _channel.Reader.ReadAllAsync())
        {
            try
            {
                if (job.CancellationToken.IsCancellationRequested)
                {
                    job.State = JobState.Cancelled;
                    Notify(job);
                    JobFinished?.Invoke(this, job);
                    continue;
                }
                await RunAsync(job);
            }
            finally
            {
                Interlocked.Decrement(ref _busy);
            }
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (IOException) { /* a viewer may still hold a temp file; the OS cleans temp eventually */ }
        catch (UnauthorizedAccessException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _worker;
    }

    /// <summary>Allocates final output paths according to the preset's <see cref="OutputRule"/>.</summary>
    private sealed class FinalAllocator(JobQueue queue, OutputRule rule, OutputPathResolver.Tokens tokens) : IOutputAllocator
    {
        public string? Allocate(string extension, string? suffix = null)
        {
            // Reserve under a lock so two parallel files never pick the same name.
            lock (queue._reservedOutputs)
            {
                var path = OutputPathResolver.Resolve(rule, tokens, extension,
                    p => File.Exists(p) || Directory.Exists(p) || queue._reservedOutputs.Contains(p), suffix);
                if (path is null)
                    return null;

                queue._reservedOutputs.Add(path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // An empty extension asks for a folder (archive extraction): the converter creates it.
                return path;
            }
        }
    }

    /// <summary>Allocates unique paths inside the job's work directory for intermediate files.</summary>
    private sealed class TempAllocator(string workDir, string baseName) : IOutputAllocator
    {
        public string? Allocate(string extension, string? suffix = null)
        {
            var dir = Path.Combine(workDir, Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            var ext = extension.TrimStart('.');
            return Path.Combine(dir, ext.Length == 0 ? $"{baseName}{suffix}" : $"{baseName}{suffix}.{ext}");
        }
    }
}
