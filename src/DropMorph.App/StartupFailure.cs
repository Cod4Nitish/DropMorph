// A start that fails before any window exists (Avalonia could not initialise): written to the log and shown in a
// system alert, since DropMorph has no window of its own to show it in.

using System.Diagnostics;
using DropMorph.Core.Settings;

namespace DropMorph.App;

internal static class StartupFailure
{
    public static void Report(Exception exception, string message)
    {
        try
        {
            var logs = Path.Combine(UserDataStore.DefaultDirectory, "logs");
            Directory.CreateDirectory(logs);
            File.AppendAllText(Path.Combine(logs, $"dropmorph-{DateTime.Now:yyyyMMdd}.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [Error] Startup: {message}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        if (!OperatingSystem.IsMacOS())
            return;
        try
        {
            // osascript shows the alert even though this process has no window; the text goes in as an argument.
            using var alert = Process.Start(new ProcessStartInfo("/usr/bin/osascript")
            {
                ArgumentList =
                {
                    "-e", "on run argv",
                    "-e", "display alert \"DropMorph\" message (item 1 of argv) as critical buttons {\"OK\"}",
                    "-e", "end run",
                    message,
                },
                UseShellExecute = false,
            });
            alert?.WaitForExit(TimeSpan.FromMinutes(10));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }
}
