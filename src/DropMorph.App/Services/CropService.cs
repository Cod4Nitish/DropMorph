// Loads image previews and performs crops using ImageMagick and OutputPathResolver.

using System.IO;
using Avalonia.Media.Imaging;
using DropMorph.Core.Localization;
using DropMorph.Core.Presets;
using DropMorph.Core.Settings;
using DropMorph.Engines.Magick;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace DropMorph.App.Services;

public enum CropStatus
{
    Success,
    SkippedExists,
    Failed,
}

public sealed record CropResult(CropStatus Status, string? OutputPath, string? ErrorKey, string? ErrorDetail);

public static class CropService
{
    private static MagickReadSettings CreateReadSettings(string inputPath)
    {
        var ext = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
        var settings = new MagickReadSettings();
        if (ImageEncoder.ExplicitCoder.Contains(ext))
            settings.Format = ImageEncoder.ToMagickFormat(ext);
        return settings;
    }

    private static void EnsureNotAnimated(string inputPath)
    {
        if (inputPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && Apng.IsAnimated(inputPath))
            throw new NotSupportedException("Animated PNG files cannot be cropped interactively.");

        var readSettings = CreateReadSettings(inputPath);
        var infos = readSettings.Format != MagickFormat.Unknown
            ? MagickImageInfo.ReadCollection(inputPath, readSettings).ToList()
            : MagickImageInfo.ReadCollection(inputPath).ToList();

        if (infos.Count > 1)
            throw new NotSupportedException("Multi-frame / animated images cannot be cropped interactively.");
    }

    /// <summary>
    /// Loads an image as a standard Avalonia Bitmap along with its true pixel dimensions,
    /// properly oriented according to EXIF.
    /// </summary>
    public static async Task<(Bitmap Bitmap, int Width, int Height)> LoadPreviewAsync(string inputPath)
    {
        return await Task.Run(() =>
        {
            EnsureNotAnimated(inputPath);

            var readSettings = CreateReadSettings(inputPath);
            using var image = readSettings.Format != MagickFormat.Unknown
                ? new MagickImage(inputPath, readSettings)
                : new MagickImage(inputPath);

            image.AutoOrient();
            var origWidth = (int)image.Width;
            var origHeight = (int)image.Height;

            // Generate an in-memory PNG for the viewport, downscaling huge images for UI fluidity.
            const int maxDisplayDim = 2560;
            if (image.Width > maxDisplayDim || image.Height > maxDisplayDim)
            {
                image.Resize(new MagickGeometry((uint)maxDisplayDim, (uint)maxDisplayDim) { Greater = true });
            }

            using var mem = new MemoryStream();
            image.Write(mem, MagickFormat.Png);
            mem.Position = 0;
            var bitmap = new Bitmap(mem);

            return (bitmap, origWidth, origHeight);
        });
    }

    /// <summary>
    /// Crops the image at <paramref name="inputPath"/> with the given pixel coordinates and saves the result using
    /// the preset's output rule and the application's default output settings.
    /// </summary>
    public static async Task<CropResult> CropFileAsync(
        string inputPath,
        int x, int y, int width, int height,
        Preset preset,
        UserDataStore store,
        ILocalizer loc,
        ILogger? log = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                EnsureNotAnimated(inputPath);

                var ext = Path.GetExtension(inputPath).TrimStart('.');
                var appliedPreset = store.Settings.WithDefaultOutput(preset);
                var tokens = new OutputPathResolver.Tokens(inputPath, loc.DisplayName(appliedPreset), 1, DateTime.Now);
                var outputPath = OutputPathResolver.Resolve(appliedPreset.Output, tokens, ext, File.Exists);
                if (outputPath is null)
                {
                    log?.LogInformation("Skipping crop of {Input} as conflict policy resolved to null (file exists)", inputPath);
                    return new CropResult(CropStatus.SkippedExists, null, "toast.skipped", Path.GetFileName(inputPath));
                }

                var outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir))
                    Directory.CreateDirectory(outDir);

                var readSettings = CreateReadSettings(inputPath);
                using var image = readSettings.Format != MagickFormat.Unknown
                    ? new MagickImage(inputPath, readSettings)
                    : new MagickImage(inputPath);

                image.AutoOrient();

                var clampedX = Math.Clamp(x, 0, Math.Max(0, (int)image.Width - 1));
                var clampedY = Math.Clamp(y, 0, Math.Max(0, (int)image.Height - 1));
                var clampedW = (uint)Math.Clamp(width, 1, Math.Max(1, (int)image.Width - clampedX));
                var clampedH = (uint)Math.Clamp(height, 1, Math.Max(1, (int)image.Height - clampedY));

                image.Crop(new MagickGeometry(clampedX, clampedY, clampedW, clampedH));
                image.ResetPage();

                // Drop EXIF thumbnail so viewers don't show the uncropped preview
                if (image.GetExifProfile() is { } exif && exif.ThumbnailLength > 0)
                {
                    exif.RemoveThumbnail();
                    image.SetProfile(exif);
                }

                image.Write(outputPath);

                if (appliedPreset.Output.KeepDates || store.Settings.KeepFileDates)
                {
                    try
                    {
                        File.SetCreationTimeUtc(outputPath, File.GetCreationTimeUtc(inputPath));
                        File.SetLastWriteTimeUtc(outputPath, File.GetLastWriteTimeUtc(inputPath));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        log?.LogDebug(ex, "Could not preserve file timestamps for {Path}", outputPath);
                    }
                }

                return new CropResult(CropStatus.Success, outputPath, null, null);
            }
            catch (NotSupportedException ex)
            {
                log?.LogInformation(ex, "Not cropping {File}", inputPath);
                return new CropResult(CropStatus.Failed, null, "error.unsupported_source", null);
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "Failed to crop {File}", inputPath);
                return new CropResult(CropStatus.Failed, null, "error.conversion_failed", ex.Message);
            }
        });
    }
}
