// Video and audio conversion with FFmpeg (GPL-3.0 build, an optional download run as a separate program in
// engines/ffmpeg): video ↔ video (H.264, HEVC, AV1, …), video → animated GIF / APNG, a still frame or every frame
// (FrameSequenceWriter), audio extraction, subtitle extraction (the first text track as SRT / ASS / WebVTT), audio ↔
// audio and GIF, APNG or a still picture → video, and images → HEIC (FFmpeg's HEVC encoder, DropMorph's own container
// writer). Container and codec choices live in MediaEncoding, the process handling in FfmpegRunner, hardware encoders
// in HardwareVideo (options) and HardwareProbe (which one works here).

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DropMorph.Core.Conversion;
using DropMorph.Core.Formats;
using DropMorph.Core.Presets;
using DropMorph.Engines.Heic;
using DropMorph.Engines.Infrastructure;
using DropMorph.Engines.Magick;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DropMorph.Engines.Media;

/// <summary>Converts video and audio files with DropMorph's own copy of FFmpeg.</summary>
/// <param name="logger">Receives which hardware encoders work and why a hardware encode was redone in software.</param>
public sealed partial class FfmpegConverter(ILogger? logger = null) : IConverter, IImageTransformer
{
    private static readonly ConcurrentDictionary<string, string> Versions = new();

    /// <summary><c>ffmpeg -version</c> and <c>-encoders</c> answer at once; a copy that does not is broken or hangs.</summary>
    internal static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger = logger ?? NullLogger.Instance;

    private (string Ffmpeg, string Ffprobe)? _tools;

    /// <summary>
    /// Picks the hardware encoder for (ffmpeg, codec, 10-bit, encoders) instead of <see cref="HardwareProbe.SelectAsync"/>.
    /// Tests put an encoder here that FFmpeg does not have, to see the software retry.
    /// </summary>
    internal Func<string, string, bool, IReadOnlySet<string>?, CancellationToken, Task<HardwareEncoder?>>? SelectHardware { get; init; }

    public string Id => "ffmpeg";
    public string DisplayName => "FFmpeg";

    /// <summary>FFmpeg's encoders use every core themselves; two runs keep the CPU busy between files.</summary>
    public int MaxParallelism => 2;

    /// <summary>Only HEIC, which ImageMagick reads and turns before x265 encodes it (Heic/HeicWriter); never video.</summary>
    public bool Transforms(string from, string to) => to == "heic";

    public IReadOnlyList<ConversionEdge> Edges { get; } = BuildEdges();

    /// <summary>
    /// Every video format (read by FFmpeg's demuxers) → every video target, animated GIF or APNG, a still PNG frame,
    /// every frame as PNG or JPG pictures (only when a preset asks for it, see <see cref="ConversionEdge.FrameSequence"/>),
    /// every audio target and SRT / ASS / WebVTT (its subtitles); every audio format → every audio target (including
    /// itself: re-encode with another bitrate); GIF, APNG and PNG → MP4, WEBM, MOV; every image ImageMagick reads →
    /// HEIC. Only formats the pinned build can write are targets (see MediaEncoding).
    /// </summary>
    /// <remarks>
    /// The GIF and APNG edges cost more (slow, large), so video → JPG / PDF / TIFF … goes through the still frame
    /// (video → PNG → image engines) and never renders an animation of a whole film just to take one frame of it.
    /// The other way, a photo (HEIC, JPG, …) reaches video as a full-colour PNG: the image engines' still → GIF and
    /// still → APNG edges cost 12 (<see cref="Magick.ImageEncoder.Edge"/>), so 10 + 11 beats 12 + 10 and 12 + 11.
    /// Animated GIF/WebP and PDF or TIFF pages keep the GIF route (10 + 10, APNG costs 10 + 11), which keeps their
    /// frames; an APNG goes straight to video.
    /// </remarks>
    private static List<ConversionEdge> BuildEdges()
    {
        var videos = FormatRegistry.Known.Where(f => f.Category == FormatCategory.Video).Select(f => f.Id).ToList();
        var audios = FormatRegistry.Known.Where(f => f.Category == FormatCategory.Audio).Select(f => f.Id);
        var videoTargets = MediaEncoding.VideoTargets.Keys.Concat(MediaEncoding.AudioTargets.Keys).Append("png").ToList();
        return
        [
            .. from source in videos from target in videoTargets select new ConversionEdge(source, target),
            // "Save every frame": a folder of numbered pictures, only when a preset asks for it (never in another route).
            .. from source in videos from target in MediaOptions.FrameTargets select new ConversionEdge(source, target, FrameSequence: true),
            // Subtitles out of a video. SRT is the hub to the other subtitle formats (the subtitle engine takes it on),
            // so ASS and WebVTT cost a little more and are only taken when they are the target.
            .. from source in videos from target in MediaEncoding.SubtitleTargets.Keys
               select new ConversionEdge(source, target, target == "srt" ? 10 : 11),
            .. videos.Select(source => new ConversionEdge(source, "gif", 15)),
            .. videos.Select(source => new ConversionEdge(source, "apng", 15)),
            .. from source in audios from target in MediaEncoding.AudioTargets.Keys select new ConversionEdge(source, target),
            .. MediaEncoding.PictureToVideoTargets.Select(target => new ConversionEdge("gif", target)),
            .. MediaEncoding.PictureToVideoTargets.Select(target => new ConversionEdge("apng", target, 11)),
            .. MediaEncoding.PictureToVideoTargets.Select(target => new ConversionEdge("png", target, 11)),
            // HEIC: ImageMagick reads, x265 encodes, DropMorph writes the container (see Heic/HeicWriter).
            .. ImageEncoder.Sources.Select(source => new ConversionEdge(source, "heic")),
        ];
    }

    public EngineStatus GetStatus()
    {
        _tools = Locate();
        if (_tools is not { } tools)
            return EngineStatus.Unavailable("engine.reason.not_installed");
        try
        {
            return EngineStatus.Available(tools.Ffmpeg, $"FFmpeg {VersionOf(tools.Ffmpeg)}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            // Present but broken, e.g. a DLL next to ffmpeg.exe is missing.
            return EngineStatus.Unavailable("engine.reason.error", ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var (ffmpeg, ffprobe) = _tools ?? Locate() ?? throw new InvalidOperationException("FFmpeg was not found.");
        if (step.To == "heic")
            return await HeicWriter.ConvertAsync(ffmpeg, step, progress, cancellationToken);
        var input = Path.GetFullPath(step.InputPath);
        var info = await MediaInfo.ProbeAsync(ffprobe, input, cancellationToken);
        progress?.Report(0.02);

        if (step.FrameSequence)
            return await FrameSequenceWriter.ConvertAsync(ffmpeg, step, input, info, progress, cancellationToken);

        var output = step.Output.Allocate(FormatRegistry.Get(step.To).PrimaryExtension);
        if (output is null)
            return [];

        // A PNG is a still picture (from the image engines), shown for a few seconds.
        var still = step.From == "png";

        // Album art for targets FFmpeg can't put a picture stream into (Ogg, Matroska): prepared on the side.
        var cover = !still && info.CoverStreamIndex is { } coverStream && MediaEncoding.AudioTargets.ContainsKey(step.To)
            ? await CoverArt.PrepareAsync(ffmpeg, input, coverStream, step.To, step.WorkDirectory, cancellationToken)
            : null;

        var media = step.Preset.Media;
        var encoders = still ? null : EncodersOf(ffmpeg);
        // Hardware encoding (preset option): the first GPU encoder that passes its test encode, if any.
        HardwareEncoder? hardware = null;
        if (!still && MediaEncoding.HardwareCodec(step.To, media, info) is { } codec)
        {
            var tenBit = HardwareVideo.TenBit(codec, info.Video!);
            hardware = SelectHardware is { } select
                ? await select(ffmpeg, codec, tenBit, encoders, cancellationToken)
                : await HardwareProbe.SelectAsync(ffmpeg, codec, tenBit, encoders, _logger, cancellationToken);
        }

        // Video → animated GIF / APNG can play faster or slower (the image options' playback speed).
        var speed = step.To is "gif" or "apng" ? step.Preset.Image.PlaybackSpeed : 1;
        IReadOnlyList<FfmpegPass> Plan(HardwareEncoder? encoder) => still
            ? MediaEncoding.PlanStill(input, output, step.To, media, info)
            : MediaEncoding.Plan(input, output, step.To, media, info, step.WorkDirectory, cover, encoders, encoder, speed);
        // Throws for a missing stream before anything is written (an existing file of that name stays untouched).
        var passes = Plan(hardware);
        // FFmpeg reports the position in the output, which a speed change makes longer or shorter.
        var duration = still ? MediaEncoding.StillDuration : info.Duration / speed;
        try
        {
            await RunWithFallbackAsync(passes, hardware, Plan,
                planned => RunPassesAsync(ffmpeg, planned, output, duration, progress, cancellationToken), output, _logger, cancellationToken);
        }
        catch
        {
            // Never leave a half-written file where the user expects a finished one.
            TryDelete(output);
            throw;
        }

        progress?.Report(1);
        return [output];
    }

    /// <summary>
    /// Runs <paramref name="passes"/>, planned with <paramref name="hardware"/>. When a hardware encode fails (a driver
    /// error, a picture larger than the chip encodes, …) the file is encoded again as <paramref name="plan"/> plans it
    /// without hardware: an encoder that passed its test encode can still refuse one particular video, and the user
    /// asked for a converted file, not for the GPU. The failure is logged, not reported. Cancellation is never retried.
    /// </summary>
    internal static async Task RunWithFallbackAsync(IReadOnlyList<FfmpegPass> passes, HardwareEncoder? hardware,
        Func<HardwareEncoder?, IReadOnlyList<FfmpegPass>> plan, Func<IReadOnlyList<FfmpegPass>, Task> run, string output, ILogger logger,
        CancellationToken cancellationToken)
    {
        if (hardware is null)
        {
            await run(passes);
            return;
        }
        try
        {
            await run(passes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("{File}: hardware encoder {Encoder} failed ({Reason}); encoding it again with the software encoder",
                output, hardware.Name, ex.Message);
            TryDelete(output);
            // An FFmpeg without x265 says so here: the hardware encoder was the only way to write HEVC.
            await run(plan(null));
        }
    }

    /// <summary>Runs every pass, then checks that FFmpeg wrote something.</summary>
    private static async Task RunPassesAsync(string ffmpeg, IReadOnlyList<FfmpegPass> passes, string output, TimeSpan? duration,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var done = 0.0;
        foreach (var pass in passes)
        {
            await RunPassAsync(ffmpeg, pass, duration, progress, done, cancellationToken);
            done += pass.Weight;
        }
        if (!File.Exists(output) || new FileInfo(output).Length == 0)
            throw new InvalidOperationException("FFmpeg did not write any output.");
    }

    /// <summary>Runs one pass and maps FFmpeg's position (seconds) onto this pass' share of the progress.</summary>
    internal static async Task RunPassAsync(string ffmpeg, FfmpegPass pass, TimeSpan? duration, IProgress<double>? progress, double done, CancellationToken cancellationToken)
    {
        var total = duration?.TotalSeconds ?? 0;
        Action<double>? onPosition = progress is null || total <= 0
            ? null
            : seconds => progress.Report(0.02 + 0.97 * (done + pass.Weight * Math.Clamp(seconds / total, 0, 1)));

        ProcessResult result;
        // Without a duration (raw streams) there is nothing to measure against: show an estimate instead.
        using (total <= 0 ? ProgressEstimate.Start(progress, TimeSpan.FromSeconds(20), 0.02 + 0.97 * done, 0.02 + 0.97 * (done + pass.Weight)) : null)
            result = await FfmpegRunner.RunAsync(ffmpeg, pass.Arguments, onPosition, cancellationToken);

        if (result.ExitCode != 0)
        {
            var reason = FfmpegRunner.Summarize(result.StandardError);
            throw new InvalidOperationException($"FFmpeg failed (exit code {result.ExitCode}){(reason.Length > 0 ? ": " + reason : ".")}");
        }
    }

    /// <summary>Both tools must come from the same installation.</summary>
    internal static (string Ffmpeg, string Ffprobe)? Locate()
    {
        var folder = EngineEnvironment.OwnCopyFolder("ffmpeg")
            ?? (EngineEnvironment.FindBundled("ffmpeg") is { } bundled ? EngineEnvironment.FolderWithPrograms("ffmpeg", bundled) : null)
            ?? EngineEnvironment.SystemCopyFolder("ffmpeg");
        return folder is null ? null : (Path.Combine(folder, EngineEnvironment.ProgramName("ffmpeg.exe")),
            Path.Combine(folder, EngineEnvironment.ProgramName("ffprobe.exe")));
    }

    /// <summary>"9.0.2" from <c>ffmpeg -version</c>; cached per executable so re-checking the engines stays cheap.</summary>
    /// <exception cref="InvalidOperationException">FFmpeg does not start, fails or does not answer within <paramref name="timeout"/>.</exception>
    internal static string VersionOf(string ffmpeg, TimeSpan? timeout = null) =>
        Versions.GetOrAdd($"{ffmpeg}|{File.GetLastWriteTimeUtc(ffmpeg).Ticks}", _ =>
        {
            ProcessResult result;
            try
            {
                result = FfmpegRunner.RunBriefly(ffmpeg, ["-hide_banner", "-version"], timeout ?? QueryTimeout);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("FFmpeg did not start in time.");
            }
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg does not start (exit code {result.ExitCode}).");
            // "ffmpeg version 9.0.2-full_build-www.gyan.dev Copyright …" → "9.0.2"
            var firstLine = result.StandardOutput.Split('\n', 2)[0];
            var match = VersionPattern().Match(firstLine);
            return match.Success ? match.Groups[1].Value : firstLine.Trim();
        });

    [GeneratedRegex(@"version\s+n?(\d+(?:\.\d+)+|\S+)")]
    private static partial Regex VersionPattern();

    private static readonly ConcurrentDictionary<string, IReadOnlySet<string>?> Encoders = new();

    /// <summary>
    /// The encoders of <paramref name="ffmpeg"/> (<c>ffmpeg -encoders</c>), cached per executable: a copy the user
    /// picked may lack x265 or SVT-AV1. Null (assume DropMorph's build) when the list can't be read.
    /// </summary>
    internal static IReadOnlySet<string>? EncodersOf(string ffmpeg, TimeSpan? timeout = null) =>
        Encoders.GetOrAdd($"{ffmpeg}|{File.GetLastWriteTimeUtc(ffmpeg).Ticks}", _ =>
        {
            try
            {
                var result = FfmpegRunner.RunBriefly(ffmpeg, ["-hide_banner", "-encoders"], timeout ?? QueryTimeout);
                return result.ExitCode == 0 ? ParseEncoders(result.StandardOutput) : null;
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
            {
                return null;
            }
        });

    /// <summary>The encoder names in <c>ffmpeg -encoders</c> output.</summary>
    internal static IReadOnlySet<string> ParseEncoders(string list) =>
        // " V....D libx265              libx265 H.265 / HEVC (codec hevc)"
        list.Split('\n')
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[0].Length == 6 && parts[0][0] is 'V' or 'A' or 'S')
            .Select(parts => parts[1])
            .ToHashSet(StringComparer.Ordinal);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
