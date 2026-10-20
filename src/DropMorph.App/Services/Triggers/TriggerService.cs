// Connects the global input hook (SharpHook / libuiohook) to the GestureDetector and forwards detected
// gestures to the UI thread. Hook callbacks must return quickly: Windows silently removes a low-level hook
// whose callback takes longer than ~1 s, so no UI work happens here.

using System.Diagnostics;
using Avalonia.Threading;
using DropMorph.Core.Platform;
using DropMorph.Core.Settings;
using Microsoft.Extensions.Logging;
using SharpHook;
using SharpHook.Data;

namespace DropMorph.App.Services.Triggers;

/// <summary>Global gesture detection. Events are raised on the UI thread with physical screen coordinates.</summary>
public sealed class TriggerService : IDisposable
{
    private readonly IPlatformServices _platform;
    private readonly ILogger<TriggerService> _log;
    private readonly GestureDetector _detector = new();
    private readonly string _ownProcess = Process.GetCurrentProcess().ProcessName;
    private EventLoopGlobalHook? _hook;
    private Thread? _hookThread;
    private readonly Func<EventLoopGlobalHook> _createHook;
    private HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>DROPMORPH_TRACE_GESTURES=1 logs each mouse press and scope decision, to find why a gesture doesn't fire.</summary>
    private static readonly bool Trace = Environment.GetEnvironmentVariable("DROPMORPH_TRACE_GESTURES") == "1";

    public TriggerService(IPlatformServices platform, ILogger<TriggerService> log)
        : this(platform, log, () => new EventLoopGlobalHook(useBackgroundThreadForEventLoop: true))
    {
    }

    internal TriggerService(IPlatformServices platform, ILogger<TriggerService> log, Func<EventLoopGlobalHook> createHook)
    {
        _platform = platform;
        _log = log;
        _createHook = createHook;
        _detector.SystemDragThreshold = platform.SystemDragThreshold;
        _detector.ScopeFilter = InScope;

        _detector.DragStarted += (g, x, y) => Post(() => DragStarted?.Invoke(this, new GestureEventArgs(g, x, y)));
        _detector.DragEnded += (x, y) => Post(() => DragEnded?.Invoke(this, new GestureEventArgs(null, x, y)));
        _detector.HoldTriggered += (g, x, y) => Post(() => SelectionGesture?.Invoke(this, new GestureEventArgs(g, x, y)));
        _detector.KeyChordTriggered += (g, x, y) => Post(() => SelectionGesture?.Invoke(this, new GestureEventArgs(g, x, y)));
        _detector.HoldTimerRequested += (token, ms) =>
            _ = Task.Delay(ms).ContinueWith(_ => _detector.HoldElapsed(token), TaskScheduler.Default);
    }

    /// <summary>A modifier+drag started (files may be dragged). Show the donut and wait for DragEnter.</summary>
    public event EventHandler<GestureEventArgs>? DragStarted;

    /// <summary>The mouse button of an active drag gesture was released.</summary>
    public event EventHandler<GestureEventArgs>? DragEnded;

    /// <summary>A hold or keyboard gesture: open the donut for the file manager's current selection.</summary>
    public event EventHandler<GestureEventArgs>? SelectionGesture;

    /// <summary>Last known cursor position (physical pixels).</summary>
    public (int X, int Y) LastCursor { get; private set; }

    public bool IsRunning => _hook?.IsRunning == true;
    public string? Error { get; private set; }

    /// <summary>
    /// Linux: libuiohook could not load its X11 back end (libuiohook-xrecord.so), usually a missing X library
    /// (libXtst, libXt) or not an X11 session. <see cref="Error"/> keeps the raw message.
    /// </summary>
    public bool LinuxBackendMissing { get; private set; }

    public event EventHandler? StatusChanged;

    private void NotifyStatus() => Post(() => StatusChanged?.Invoke(this, EventArgs.Empty));

    public void Apply(AppSettings settings)
    {
        _detector.Configure(settings.Triggers.Where(g => _platform.SupportsSelectionShortcut || g.Kind == TriggerKind.Drag).ToList());
        _detector.Paused = settings.Paused;
        _excluded = new HashSet<string>(settings.ExcludedProcesses.Select(p => p.Trim().Replace(".exe", "", StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>macOS without Accessibility permission: gestures wait for it (and start by themselves once it is given).</summary>
    public bool NeedsPermission { get; private set; }

    private Timer? _permissionWatch;

    public void Start()
    {
        if (_hook is not null)
            return;
        if (!_platform.SupportsGlobalPointerGestures)
        {
            if (OperatingSystem.IsMacOS())
                WatchForPermission();
            return;
        }
        if (NeedsPermission)
        {
            NeedsPermission = false;
            NotifyStatus();
        }
        Error = null;
        LinuxBackendMissing = false;
        try
        {
            // Background threads: a hook that fails to stop can never keep DropMorph running after it quits.
            _hook = _createHook();
            _hook.HookEnabled += (_, _) => { _log.LogInformation("Global input hook started"); NotifyStatus(); };
            _hook.HookDisabled += (_, _) => { _log.LogInformation("Global input hook stopped"); NotifyStatus(); };
            _hook.MousePressed += (_, e) =>
            {
                if (Trace)
                    _log.LogInformation("Trace: press {Button} at {X},{Y}, mask {Mask} = {Modifiers}", e.Data.Button, e.Data.X, e.Data.Y, e.RawEvent.Mask, Map(e.RawEvent.Mask));
                var (x, y) = ButtonPoint(e);
                _detector.MouseDown(Map(e.Data.Button), x, y, Map(e.RawEvent.Mask));
            };
            _hook.MouseReleased += (_, e) =>
            {
                var (x, y) = ButtonPoint(e);
                _detector.MouseUp(Map(e.Data.Button), x, y);
            };
            _hook.MouseDragged += (_, e) => OnMove(e);
            _hook.MouseMoved += (_, e) => OnMove(e);
            _hook.MouseDraggedRelative += (_, e) => OnRelativeMove(e);
            _hook.MouseMovedRelative += (_, e) => OnRelativeMove(e);
            _hook.KeyPressed += (_, e) => _detector.KeyDown(KeyName(e.Data.KeyCode), Map(e.RawEvent.Mask));
            // Own the actual Run thread. SharpHook's RunAsync task completes BEFORE its finally
            // clears the native callback, so awaiting that task alone is not a safe restart barrier.
            var hook = _hook;
            _hookThread = new Thread(() =>
            {
                try
                {
                    hook.Run();
                }
                catch (Exception ex)
                {
                    ReportFailure(ex.GetBaseException(), "Global input hook failed; gestures are unavailable");
                }
                finally
                {
                    NotifyStatus();
                }
            })
            { IsBackground = true, Name = "DropMorph global input" };
            _hookThread.Start();
        }
        catch (Exception ex)
        {
            // e.g. macOS without Accessibility permission. The app keeps working via the drop zone / context menu.
            ReportFailure(ex, "Could not start the global input hook");
            _hook?.Dispose();
            _hook = null;
            _hookThread = null;
            NotifyStatus();
        }
    }

    private void ReportFailure(Exception exception, string message)
    {
        // The raw libuiohook result goes to the log either way (the packaged-app smoke test looks for it).
        var result = (exception as HookException)?.Result.ToString() ?? "none";
        _log.LogError(exception, "{Message} (libuiohook result: {Result})", message, result);
        LinuxBackendMissing = OperatingSystem.IsLinux() && IsMissingLinuxBackend(exception);
        Error = exception.Message;
    }

    /// <summary>libuiohook's loader could not dlopen its Linux back end (ErrorLinuxLoadBackend).</summary>
    internal static bool IsMissingLinuxBackend(Exception exception) =>
        exception is HookException { Result: UioHookResult.ErrorLinuxLoadBackend };

    // Wayland (experimental): presses carry no position and a mouse only moves relatively, while tablets and virtual
    // machines still send absolute moves. So a running estimate is kept: absolute moves set it, relative moves add to
    // it, and presses start from it. It is only good for the drag threshold and drifts over a long hover. Placing the
    // donut at the real cursor is a KWin script's job (installer/linux/kwin), and LastCursor is left alone because
    // the estimate is not a screen position.
    private int _estX, _estY;

    private (int X, int Y) ButtonPoint(MouseHookEventArgs e) =>
        _platform.PressCarriesPosition ? (e.Data.X, e.Data.Y) : (_estX, _estY);

    private void OnMove(MouseHookEventArgs e)
    {
        (_estX, _estY) = (e.Data.X, e.Data.Y);
        LastCursor = (e.Data.X, e.Data.Y);
        _detector.MouseMove(e.Data.X, e.Data.Y, Map(e.RawEvent.Mask), HeldButton(e.RawEvent.Mask));
    }

    private void OnRelativeMove(MouseHookEventArgs e)
    {
        _estX += e.Data.X;
        _estY += e.Data.Y;
        if (Trace && HeldButton(e.RawEvent.Mask) != TriggerMouseButton.None)
            _log.LogInformation("Trace: relative move {Dx},{Dy} -> {X},{Y}, mask {Mask}", e.Data.X, e.Data.Y, _estX, _estY, e.RawEvent.Mask);
        _detector.MouseMove(_estX, _estY, Map(e.RawEvent.Mask), HeldButton(e.RawEvent.Mask));
    }

    private bool InScope(TriggerGesture gesture, int x, int y)
    {
        var process = _platform.ProcessNameAt(x, y);
        if (Trace)
            _log.LogInformation("Trace: {Gesture} at {X},{Y} over {Process}", gesture.Kind, x, y, process ?? "(none)");
        if (process is not null && (string.Equals(process, _ownProcess, StringComparison.OrdinalIgnoreCase) || _excluded.Contains(process)))
            return false;
        return gesture.Scope == TriggerScope.Anywhere || _platform.IsFileManagerAt(x, y);
    }

    private static void Post(Action action) => Dispatcher.UIThread.Post(action);

    internal static TriggerMouseButton Map(MouseButton button) => button switch
    {
        MouseButton.Button1 => TriggerMouseButton.Left,
        MouseButton.Button2 => TriggerMouseButton.Right,
        MouseButton.Button3 => TriggerMouseButton.Middle,
        MouseButton.Button4 => TriggerMouseButton.X1,
        MouseButton.Button5 => TriggerMouseButton.X2,
        _ => TriggerMouseButton.None,
    };

    internal static ModifierKeys Map(EventMask mask)
    {
        var result = ModifierKeys.None;
        if (mask.HasCtrl()) result |= ModifierKeys.Ctrl;
        if (mask.HasAlt()) result |= ModifierKeys.Alt;
        if (mask.HasShift()) result |= ModifierKeys.Shift;
        if (mask.HasMeta()) result |= ModifierKeys.Meta;
        return result;
    }

    internal static TriggerMouseButton HeldButton(EventMask mask) =>
        mask.HasFlag(EventMask.Button1) ? TriggerMouseButton.Left
        : mask.HasFlag(EventMask.Button2) ? TriggerMouseButton.Right
        : mask.HasFlag(EventMask.Button3) ? TriggerMouseButton.Middle
        : mask.HasFlag(EventMask.Button4) ? TriggerMouseButton.X1
        : mask.HasFlag(EventMask.Button5) ? TriggerMouseButton.X2
        : TriggerMouseButton.None;

    /// <summary>"VcSpace" → "Space".</summary>
    internal static string KeyName(KeyCode code)
    {
        var name = code.ToString();
        return name.StartsWith("Vc", StringComparison.Ordinal) ? name[2..] : name;
    }

    /// <summary>
    /// Checks every two seconds whether the user switched DropMorph on under Accessibility, then starts the gestures;
    /// no restart needed.
    /// </summary>
    private void WatchForPermission()
    {
        if (!NeedsPermission)
        {
            NeedsPermission = true;
            _log.LogInformation("Gestures wait for the Accessibility permission");
            NotifyStatus();
        }
        _permissionWatch ??= new Timer(_ =>
        {
            if (!_platform.SupportsGlobalPointerGestures)
                return;
            _permissionWatch?.Dispose();
            _permissionWatch = null;
            _log.LogInformation("Accessibility permission granted");
            Post(Start);
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        _permissionWatch?.Dispose();
        _permissionWatch = null;
        try
        {
            _hook?.Dispose();
        }
        catch (HookException ex)
        {
            // Quitting goes on: the hook runs on background threads, which end with the process.
            _log.LogWarning(ex, "Could not stop the global input hook");
            // SharpHook marks the hook disposed before Stop can throw. Its finalizer must not
            // dispose the already-completed event queue again; we still own and join its Run thread.
            if (_hook is { IsDisposed: true } failedHook)
                GC.SuppressFinalize(failedHook);
            Error = ex.Message;
            NotifyStatus();
            return; // Keep ownership: starting a second native hook would corrupt the first one.
        }
        // Dispose only requests native shutdown. Its Run thread must finish its finally block
        // before another hook installs the process-wide callback, or the old thread clears it.
        if (_hookThread is { IsAlive: true } thread && !thread.Join(TimeSpan.FromSeconds(5)))
        {
            Error = "The previous global input hook did not finish stopping. Retry after it exits.";
            _log.LogWarning("Global input hook shutdown timed out; keeping the previous hook to prevent overlap");
            NotifyStatus();
            return;
        }
        _hook = null;
        _hookThread = null;
    }
}

/// <summary>Gesture notification with physical screen coordinates.</summary>
public sealed class GestureEventArgs(TriggerGesture? gesture, int x, int y) : EventArgs
{
    public TriggerGesture? Gesture { get; } = gesture;
    public int X { get; } = x;
    public int Y { get; } = y;
}
