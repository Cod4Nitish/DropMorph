// Builds PDFs from images, splits / extracts PDF pages, merges PDFs and makes PDFs smaller (PDFsharp, MIT; the
// compression itself is in PdfCompressor, with Ghostscript as an optional second try).

using DropMorph.Core.Conversion;
using DropMorph.Core.Presets;
using DropMorph.Engines.Infrastructure;
using DropMorph.Engines.Magick;
using DropMorph.Engines.Vector;
using ImageMagick;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace DropMorph.Engines.Pdf;

/// <summary>Image → PDF, PDF → PDF (split / page range / compression) and PDF merging.</summary>
public sealed class PdfSharpConverter : IConverter, IPdfMerger, IImageTransformer
{
    private const double PointsPerInch = 72.0;
    private const double MillimetresPerInch = 25.4;

    public string Id => "pdfsharp";
    public string DisplayName => "PDFsharp";
    public int MaxParallelism => 0;

    /// <summary>Images into a PDF are turned first; a PDF to a PDF (split, extract) is copied page by page, as is.</summary>
    public bool Transforms(string from, string to) => from != "pdf";

    public IReadOnlyList<ConversionEdge> Edges { get; } =
    [
        .. ImageEncoder.Sources.Select(source => new ConversionEdge(source, "pdf")),
        new("pdf", "pdf"),
    ];

    public EngineStatus GetStatus() => EngineStatus.Available(version: EngineVersions.Library("PDFsharp", typeof(PdfDocument)));

    public Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken) =>
        step.From != "pdf" ? Task.Run(() => ImageToPdf(step, progress, cancellationToken), cancellationToken)
        : step.Preset.Pdf.Compression == PdfCompression.None ? Task.Run(() => SplitOrExtract(step, progress, cancellationToken), cancellationToken)
        : Task.Run(() => CompressAsync(step, progress, cancellationToken), cancellationToken);

    public Task MergeAsync(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            using var output = new PdfDocument();
            foreach (var input in inputPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = Open(input, PdfDocumentOpenMode.Import);
                foreach (var page in source.Pages)
                    output.AddPage(page);
            }
            output.Save(outputPath);
        }, cancellationToken);

    /// <summary>Places every frame of an image (TIFF pages, ...) on its own PDF page.</summary>
    private static IReadOnlyList<string> ImageToPdf(ConversionStep step, IProgress<double>? progress, CancellationToken ct)
    {
        var options = step.Preset.Image;
        var pdfOptions = step.Preset.Pdf;
        using var images = ImageReader.Read(step.InputPath, step.From, step.Preset);
        var frames = ImageEncoder.SelectFrames(images, step.From, "pdf");

        using var document = new PdfDocument();
        for (var i = 0; i < frames.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var frame = frames[i];
            var dpi = frame.Density.Units == DensityUnit.PixelsPerInch && frame.Density.X > 0 ? frame.Density.X
                : frame.Density.Units == DensityUnit.PixelsPerCentimeter && frame.Density.X > 0 ? frame.Density.X * 2.54
                : 96.0;

            // "pdf" as target: resize / grayscale / metadata apply, no format-specific encoding settings.
            ImageEncoder.ApplyOptions(frame, options, "pdf");
            using var stream = EncodeForPdf(frame, step.From, options);
            using var ximage = XImage.FromStream(stream);

            var imageWidthPt = frame.Width / dpi * PointsPerInch;
            var imageHeightPt = frame.Height / dpi * PointsPerInch;
            var page = document.AddPage();
            var (pageWidth, pageHeight) = PageSize(pdfOptions.PageSize, imageWidthPt > imageHeightPt);
            if (pdfOptions.PageSize == PdfPageSize.FitImage)
                (pageWidth, pageHeight) = (imageWidthPt, imageHeightPt);
            page.Width = XUnit.FromPoint(pageWidth);
            page.Height = XUnit.FromPoint(pageHeight);

            // Fit the image inside the page margins, keeping its aspect ratio, centred.
            var margin = pdfOptions.PageSize == PdfPageSize.FitImage ? 0 : pdfOptions.MarginMm / MillimetresPerInch * PointsPerInch;
            var boxWidth = pageWidth - 2 * margin;
            var boxHeight = pageHeight - 2 * margin;
            var scale = Math.Min(boxWidth / imageWidthPt, boxHeight / imageHeightPt);
            var drawWidth = imageWidthPt * scale;
            var drawHeight = imageHeightPt * scale;

            using (var gfx = XGraphics.FromPdfPage(page))
                gfx.DrawImage(ximage, margin + (boxWidth - drawWidth) / 2, margin + (boxHeight - drawHeight) / 2, drawWidth, drawHeight);

            progress?.Report((i + 1.0) / frames.Count);
        }

        var path = step.Output.Allocate("pdf");
        if (path is null)
            return [];
        document.Save(path);
        return [path];
    }

    /// <summary>PDF → PDF: split into single pages, or extract the configured page range.</summary>
    private static IReadOnlyList<string> SplitOrExtract(ConversionStep step, IProgress<double>? progress, CancellationToken ct)
    {
        using var source = Open(step.InputPath, PdfDocumentOpenMode.Import);
        var pages = PageRange.Parse(step.Preset.Pdf.PageRange, source.PageCount);
        var written = new List<string>();

        if (step.Preset.Pdf.SplitPages)
        {
            for (var i = 0; i < pages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                using var single = new PdfDocument();
                single.AddPage(source.Pages[pages[i]]);
                var path = step.Output.Allocate("pdf", $"_p{pages[i] + 1}");
                if (path is null)
                    continue;
                single.Save(path);
                written.Add(path);
                progress?.Report((i + 1.0) / pages.Count);
            }
            return written;
        }

        using var extract = new PdfDocument();
        foreach (var index in pages)
            extract.AddPage(source.Pages[index]);
        var output = step.Output.Allocate("pdf");
        if (output is null)
            return [];
        extract.Save(output);
        return [output];
    }

    /// <summary>
    /// PDF → a smaller PDF (<see cref="PdfOptions.Compression"/>), also with a page range or one file per page: the whole
    /// file keeps everything (bookmarks, form fields, links, metadata) and is compressed as it is; selected pages are
    /// copied into new PDFs first, as without compression. Each result is the smallest of the built-in compression and,
    /// when the preset asks for it and it is installed, Ghostscript's; when neither is smaller than the pages as they
    /// were, those are written unchanged and the file gets a notice.
    /// </summary>
    private static async Task<IReadOnlyList<string>> CompressAsync(ConversionStep step, IProgress<double>? progress, CancellationToken ct)
    {
        var options = step.Preset.Pdf;
        var scratch = Path.Combine(step.WorkDirectory, "compress-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(scratch);

        // The PDFs to compress: the file itself, or its selected pages copied into new PDFs.
        var parts = new List<(string Plain, string? Suffix)>();
        using (var source = Open(step.InputPath, PdfDocumentOpenMode.Import))
        {
            var pages = PageRange.Parse(options.PageRange, source.PageCount);
            if (options.SplitPages)
            {
                foreach (var index in pages)
                {
                    using var single = new PdfDocument();
                    single.AddPage(source.Pages[index]);
                    var path = Path.Combine(scratch, $"page-{index + 1}.pdf");
                    single.Save(path);
                    parts.Add((path, $"_p{index + 1}"));
                }
            }
            else if (pages.SequenceEqual(Enumerable.Range(0, source.PageCount)))
            {
                parts.Add((step.InputPath, null));
            }
            else
            {
                using var extract = new PdfDocument();
                foreach (var index in pages)
                    extract.AddPage(source.Pages[index]);
                var path = Path.Combine(scratch, "pages.pdf");
                extract.Save(path);
                parts.Add((path, null));
            }
        }

        var gs = options.CompressWithGhostscript ? GhostscriptConverter.Locate() : null;
        var written = new List<string>();
        var anySmaller = false;
        for (var i = 0; i < parts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var output = step.Output.Allocate("pdf", parts[i].Suffix);
            if (output is null)
                continue;
            anySmaller |= await CompressFileAsync(parts[i].Plain, output, step.Preset, gs, scratch, ct);
            written.Add(output);
            progress?.Report((i + 1.0) / parts.Count);
        }
        if (written.Count > 0 && !anySmaller)
            step.Notice?.Invoke("notice.pdf_not_smaller");
        return written;
    }

    /// <summary>Writes the smallest of <paramref name="plain"/> and its compressed versions to <paramref name="output"/>.</summary>
    /// <returns>True when a compressed version was smaller.</returns>
    private static async Task<bool> CompressFileAsync(string plain, string output, Preset preset, string? gs, string scratch, CancellationToken ct)
    {
        var level = preset.Pdf.Compression;
        var candidates = new List<string>();
        Exception? failure = null;
        var builtIn = Path.Combine(scratch, Guid.NewGuid().ToString("N")[..8] + "-dropmorph.pdf");
        try
        {
            using var document = OpenForChanges(plain);
            PdfCompressor.Compress(document, level, preset.Image.KeepMetadata, ct);
            document.Save(builtIn);
            candidates.Add(builtIn);
        }
        catch (Exception ex) when (gs is not null && ex is not OperationCanceledException)
        {
            failure = ex; // Ghostscript may still manage
        }

        if (gs is not null)
        {
            var rewritten = Path.Combine(scratch, Guid.NewGuid().ToString("N")[..8] + "-gs.pdf");
            try
            {
                await GhostscriptConverter.CompressPdfAsync(gs, plain, rewritten, level, scratch, ct);
                candidates.Add(rewritten);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && candidates.Count > 0)
            {
                // The built-in result stands.
            }
            catch (Exception) when (failure is not null)
            {
                throw failure;
            }
        }

        var original = new FileInfo(plain).Length;
        var best = candidates.Select(path => new FileInfo(path)).MinBy(file => file.Length);
        if (best is not null && best.Length < original)
        {
            File.Copy(best.FullName, output, overwrite: true);
            return true;
        }
        File.Copy(plain, output, overwrite: true);
        return false;
    }

    /// <summary>
    /// Opens a PDF to change it. One locked only against changes (it opens without a password) has its pages copied
    /// into a new PDF instead, as when pages are split or extracted: bookmarks and form fields stay behind then.
    /// </summary>
    private static PdfDocument OpenForChanges(string path)
    {
        try
        {
            return PdfReader.Open(path, PdfDocumentOpenMode.Modify);
        }
        catch (PdfReaderException ex) when (ex.Message.Contains("owner password", StringComparison.OrdinalIgnoreCase))
        {
            using var source = Open(path, PdfDocumentOpenMode.Import);
            var copy = new PdfDocument();
            foreach (var page in source.Pages)
                copy.AddPage(page);
            return copy;
        }
    }

    /// <summary>Opens a PDF; a password-protected one fails with the message the other PDF conversions give.</summary>
    internal static PdfDocument Open(string path, PdfDocumentOpenMode mode)
    {
        try
        {
            return PdfReader.Open(path, mode);
        }
        catch (PdfReaderException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            throw PdfDocumentReader.PasswordProtected();
        }
    }

    /// <summary>
    /// Encodes a frame for embedding. JPEG sources stay JPEG (no generational loss beyond one re-encode at high quality);
    /// everything else is embedded losslessly as PNG.
    /// </summary>
    private static MemoryStream EncodeForPdf(IMagickImage<byte> frame, string sourceFormat, ImageOptions options)
    {
        var stream = new MemoryStream();
        if (sourceFormat == "jpg" && !frame.HasAlpha)
        {
            frame.Quality = (uint)Math.Clamp(Math.Max(options.Quality, 85), 1, 100);
            frame.Write(stream, MagickFormat.Jpeg);
        }
        else
        {
            // No palette PNGs: PDFsharp's decoder rejects them with transparency, and ImageMagick writes them for
            // pictures with few colours (charts, metafiles, pixel art). Grayscale stays grayscale.
            var format = frame.HasAlpha ? MagickFormat.Png32
                : frame.ColorType is ColorType.Grayscale or ColorType.Bilevel ? MagickFormat.Png
                : MagickFormat.Png24;
            frame.Write(stream, format);
        }
        stream.Position = 0;
        return stream;
    }

    private static (double Width, double Height) PageSize(PdfPageSize size, bool landscape)
    {
        var (w, h) = size switch
        {
            PdfPageSize.Letter => (612.0, 792.0),
            _ => (595.28, 841.89), // A4
        };
        return landscape ? (h, w) : (w, h);
    }
}
