// DropMorph running as administrator while Explorer doesn't (see ElevationState): the Home page shows why gestures, drops
// and the Explorer menu do nothing, and offers to restart DropMorph with Explorer's rights. The restart lets go of the
// single-instance lock first, so the new DropMorph becomes the running one instead of handing its arguments to this one
// (which it couldn't, being refused by an elevated process anyway), then quits this DropMorph.

using CommunityToolkit.Mvvm.ComponentModel;
using DropMorph.Core.Platform;
using Microsoft.Extensions.Logging;

namespace DropMorph.App.Services;

/// <summary>Elevation state of this run and the restart without administrator rights.</summary>
public sealed partial class ElevationService : ObservableObject
{
    private readonly ILogger<ElevationService> _log;
    private readonly Func<string, string?> _relaunch;
    private readonly Action _releaseInstance;
    private readonly Action _reacquireInstance;
    private readonly Action _shutdown;

    /// <summary>The app's service: the state Program read at start-up, the real relaunch and shutdown.</summary>
    public ElevationService(ILogger<ElevationService> log) : this(log, Program.Elevation, RelaunchThroughShell,
        Program.ReleaseInstance, Program.ReacquireInstance, App.Quit)
    {
    }

    /// <param name="relaunch">Starts the executable with Explorer's rights; returns null when started, else why not.</param>
    /// <param name="releaseInstance">Lets go of the single-instance lock.</param>
    /// <param name="reacquireInstance">Takes the lock back when the restart failed.</param>
    /// <param name="shutdown">Ends this DropMorph.</param>
    internal ElevationService(ILogger<ElevationService> log, ElevationState state, Func<string, string?> relaunch,
        Action releaseInstance, Action reacquireInstance, Action shutdown)
    {
        _log = log;
        State = state;
        _relaunch = relaunch;
        _releaseInstance = releaseInstance;
        _reacquireInstance = reacquireInstance;
        _shutdown = shutdown;
    }

    public ElevationState State { get; }

    /// <summary>Explorer can't drop files on DropMorph, use its menu, or have its selection read: show the warning.</summary>
    public bool ShellCannotReachDropMorph => State.ShellCannotReachDropMorph;

    /// <summary>The last restart attempt failed (the Home page says so).</summary>
    [ObservableProperty] private bool _restartFailed;

    /// <summary>
    /// Starts DropMorph again through Explorer and quits this one. Returns false (and sets <see cref="RestartFailed"/>)
    /// when Explorer couldn't start it; this DropMorph then keeps running as before.
    /// </summary>
    public bool RestartWithoutAdministratorRights(string? executable = null)
    {
        executable ??= Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            _log.LogWarning("Restart without administrator rights: the path of DropMorph.exe is unknown");
            RestartFailed = true;
            return false;
        }

        _log.LogInformation("Restarting without administrator rights through Explorer");
        _releaseInstance();
        var error = _relaunch(executable);
        if (error is null)
        {
            RestartFailed = false;
            _shutdown();
            return true;
        }
        _log.LogWarning("Restart without administrator rights failed: {Error}", error);
        _reacquireInstance();
        RestartFailed = true;
        return false;
    }

    private static string? RelaunchThroughShell(string executable) =>
        DropMorph.Platform.Windows.Elevation.RelaunchThroughShell(executable, "", out var error) ? null : error ?? "unknown error";
}
