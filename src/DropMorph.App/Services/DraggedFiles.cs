// The local files in a drag. Avalonia reads file URLs (what Explorer, Finder and Linux file managers offer); on macOS
// some other file managers offer the older NSFilenamesPboardType array of paths instead (QSpace Pro, issue #160),
// which Avalonia passes through as a platform format of its own.

using Avalonia.Input;
using Avalonia.Platform.Storage;
using DropMorph.Core.Platform;
using DropMorph.Platform.MacOS;

namespace DropMorph.App.Services;

internal static class DraggedFiles
{
    /// <summary>Whether the drag offers files or folders in a form DropMorph reads (they may still be missing).</summary>
    public static bool Offered(IDataTransfer data) =>
        data.Contains(DataFormat.File) || data.Items.Any(item => item.Formats.Any(IsFilenames));

    /// <summary>
    /// Local file and folder paths in the drag (virtual items are ignored). A shortcut counts as the file or folder it
    /// points to. Folders go into "Compress into one archive" like on the command line (issue #186); other presets
    /// leave them out. A trailing separator is removed, and a drive or the file system root is ignored.
    /// </summary>
    public static IReadOnlyList<string> Local(IDataTransfer data) =>
        Local(data, ConversionService.Exists, AppHost.Get<IPlatformServices>().ResolveShortcuts);

    internal static IReadOnlyList<string> Local(IDataTransfer data, Func<string, bool> exists,
        Func<IReadOnlyList<string>, IReadOnlyList<string>>? resolveShortcuts = null)
    {
        IReadOnlyList<string> Usable(IReadOnlyList<string> paths) =>
            (resolveShortcuts?.Invoke(paths) ?? paths)
                .Select(path => Path.TrimEndingDirectorySeparator(path))
                .Where(path => exists(path) && !IsRoot(path))
                .Distinct(FileSystemPaths.Comparer).ToList();

        var files = Usable((data.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>().ToList());
        return files.Count > 0 ? files : Usable(Filenames(data).ToList());
    }

    /// <summary>Whether <paramref name="path"/> is a drive or the file system root ("C:\", "/").</summary>
    internal static bool IsRoot(string path) =>
        string.Equals(Path.GetPathRoot(path), path, FileSystemPaths.Comparison);

    /// <summary>The formats a drag offers, for the log when DropMorph finds no files in it.</summary>
    public static string Describe(IDataTransfer data) =>
        data.Formats.Count == 0 ? "nothing" : string.Join(", ", data.Formats.Select(format => format.Identifier).Distinct());

    private static IEnumerable<string> Filenames(IDataTransfer data) =>
        data.Items.SelectMany(item => item.Formats.Where(IsFilenames).SelectMany(format => item.TryGetRaw(format) switch
        {
            byte[] bytes => PasteboardPaths.FromPropertyList(bytes),
            string text => PasteboardPaths.FromPropertyList(text),
            _ => [],
        }));

    private static bool IsFilenames(DataFormat format) =>
        format.Kind == DataFormatKind.Platform && PasteboardPaths.IsFilenamesType(format.Identifier);
}
