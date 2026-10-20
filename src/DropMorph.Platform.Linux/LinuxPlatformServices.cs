using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using DropMorph.Core.Platform;
using DropMorph.Core.Settings;

namespace DropMorph.Platform.Linux;

[SupportedOSPlatform("linux")]
public sealed class LinuxPlatformServices : IPlatformServices
{
    private readonly LinuxDesktopIntegration _integration = new(
        XdgDirectory("XDG_CONFIG_HOME", ".config"), XdgDirectory("XDG_DATA_HOME", ".local/share"));

    private static readonly HashSet<string> FileManagers = new(StringComparer.OrdinalIgnoreCase)
    {
        "thunar", "nautilus", "org.gnome.nautilus", "nemo", "dolphin", "caja", "pcmanfm", "pcmanfm-qt", "xfdesktop",
    };

    public static bool IsWaylandSession =>
        string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    // Wayland gestures (experimental) are opt-in: DROPMORPH_WAYLAND_GESTURES=1 on KDE Plasma. libuiohook's libinput backend
    // reads /dev/input, so /dev/input is only probed once the switch is set, and it works once the user is in the
    // `input` group. Presses carry no position and a mouse only moves relatively there; a KWin script places the donut.
    private static readonly bool WaylandGestures = IsWaylandSession
        && Environment.GetEnvironmentVariable("DROPMORPH_WAYLAND_GESTURES") == "1"
        && (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").Split(':').Contains("KDE", StringComparer.OrdinalIgnoreCase)
        && CanReadInputDevices();

    private static bool CanReadInputDevices()
    {
        try
        {
            foreach (var device in Directory.EnumerateFiles("/dev/input", "event*"))
            {
                try { using var _ = File.OpenRead(device); return true; }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { }
        return false;
    }

    public bool SupportsGlobalPointerGestures => X11WindowSystem.IsAvailable || WaylandGestures;
    public bool PressCarriesPosition => !WaylandGestures;
    public bool SupportsSelectionShortcut => false;
    public string SelectionShortcutUnavailableReason =>
        "Linux file managers do not expose a shared selected-file API. Use the DropMorph context action or its Thunar shortcut.";
    public string? GlobalPointerGesturesUnavailableReason => SupportsGlobalPointerGestures ? null : IsWaylandSession
        ? "The cursor-following gesture requires an X11 desktop session. File actions and the drop zone remain available on Wayland."
        : "The cursor-following gesture requires an X11 display.";
    public string ThunarShortcutRestartRequired =>
        "Close Thunar before changing its shortcut, then reopen it to load the DropMorph action and shortcut.";

    // Wayland can't say which window is under the cursor, so any Ctrl+drag may open the (invisible) donut window;
    // it only shows when files are dragged in, and excluded apps can't be told apart there either.
    public bool IsFileManagerAt(int x, int y) =>
        WaylandGestures || ProcessNameAt(x, y) is { } name && FileManagers.Contains(name);
    public string? ProcessNameAt(int x, int y) => X11WindowSystem.ProcessNameAt(x, y);

    public IReadOnlyList<string> RunningAppNames() => IPlatformServices.AppNames(X11WindowSystem.ClientProcessNames());
    public IReadOnlyList<string> GetFileManagerSelection() => throw new NotSupportedException(SelectionShortcutUnavailableReason);
    public int SystemDragThreshold => 8;

    public bool PrefersReducedMotion
    {
        get
        {
            var result = Run("gsettings", ["get", "org.gnome.desktop.interface", "enable-animations"], allowFailure: true);
            return result?.Trim() == "false";
        }
    }

    public void SetStartWithSystem(bool enabled, string executablePath) => _integration.SetAutostart(enabled, executablePath);
    public void SetContextMenu(bool enabled, string executablePath, string label) => _integration.SetContextMenu(enabled, executablePath, label);
    public bool SetThunarShortcut(string? accelerator) => _integration.SetThunarShortcut(accelerator);
    public static string? ToThunarAccelerator(TriggerGesture gesture) => LinuxDesktopIntegration.ToThunarAccelerator(gesture);
    public ModernContextMenuState GetModernContextMenuState(string executablePath) => ModernContextMenuState.Unsupported;
    public Task<ModernContextMenuResult> SetModernContextMenuAsync(bool enabled, string executablePath) =>
        Task.FromResult(new ModernContextMenuResult(false, Error: "Use the Linux file manager context action."));

    public void RevealInFileManager(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            var uri = new Uri(fullPath).AbsoluteUri;
            var result = Run("gdbus", ["call", "--session", "--dest", "org.freedesktop.FileManager1", "--object-path",
                "/org/freedesktop/FileManager1", "--method", "org.freedesktop.FileManager1.ShowItems",
                "['" + uri.Replace("'", "\\'", StringComparison.Ordinal) + "']", ""], allowFailure: true);
            if (result is not null)
                return;
        }
        var directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
        if (directory is null || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Cannot find the folder containing '{path}'.");
        var opener = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        opener.ArgumentList.Add(directory);
        using var process = Process.Start(opener) ?? throw new InvalidOperationException("Could not start xdg-open.");
    }

    public void RaiseTopmost(nint windowHandle) => X11WindowSystem.RaiseTopmost(windowHandle);

    private static string XdgDirectory(string variable, string fallback)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        return !string.IsNullOrEmpty(configured) && Path.IsPathFullyQualified(configured)
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallback);
    }

    private static string? Run(string executable, IEnumerable<string> arguments, bool allowFailure = false)
    {
        try
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {executable}.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"{executable} did not respond within 3 seconds.");
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{executable}: {error.GetAwaiter().GetResult().Trim()}");
            return output.GetAwaiter().GetResult();
        }
        catch (Exception exception) when (allowFailure && exception is Win32Exception or InvalidOperationException or TimeoutException)
        {
            Trace.TraceInformation($"Optional Linux integration command failed: {exception.Message}");
            return null;
        }
    }
}
