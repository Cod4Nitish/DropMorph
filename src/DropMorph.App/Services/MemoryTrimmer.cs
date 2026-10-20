// Gives memory back while DropMorph waits in the tray (issue #119): a while after the last conversion finished, and after
// the main window was released. Large images grow the managed heap and the engines' caches, and the garbage collector
// keeps that memory committed for the next allocation, so DropMorph stayed hundreds of MB above its idle size.

using System.Diagnostics;
using DropMorph.Core.Conversion;
using DropMorph.Engines.Infrastructure;
using DropMorph.Platform.Windows;
using Microsoft.Extensions.Logging;

namespace DropMorph.App.Services;

public sealed class MemoryTrimmer : IDisposable
{
    /// <summary>How long no conversion may have run before memory is trimmed.</summary>
    public static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);

    private readonly Func<bool> _busy;
    private readonly Action _trim;
    private readonly TimeSpan _delay;
    private readonly Timer _timer;
    private readonly Lock _lock = new();
    private bool _requested;
    private bool _disposed;
    private int _trims;

    public MemoryTrimmer(JobQueue queue, ILogger<MemoryTrimmer> log)
        : this(() => queue.IsBusy, () => Trim(log), IdleDelay)
    {
        queue.JobFinished += (_, _) => Request();
    }

    internal MemoryTrimmer(Func<bool> busy, Action trim, TimeSpan delay)
    {
        _busy = busy;
        _trim = trim;
        _delay = delay;
        _timer = new Timer(_ => OnQuiet());
    }

    /// <summary>How often memory was trimmed.</summary>
    internal int Trims => Volatile.Read(ref _trims);

    /// <summary>
    /// Memory was freed (a job finished, the main window closed): trim once <see cref="IdleDelay"/> has passed without
    /// another request and no conversion runs. A request meanwhile moves the time on; nothing happens without one.
    /// </summary>
    public void Request()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _requested = true;
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnQuiet()
    {
        lock (_lock)
        {
            if (!_requested || _disposed)
                return;
            if (_busy())
            {
                // A conversion started meanwhile: never during one. Look again later (its end requests too).
                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
                return;
            }
            _requested = false;
        }
        _trim();
        Interlocked.Increment(ref _trims);
    }

    /// <summary>Drops the engines' caches, collects and compacts every heap, then trims the working set (Windows).</summary>
    internal static void Trim(ILogger log)
    {
        try
        {
            var watch = Stopwatch.StartNew();
            var before = PrivateMegabytes();
            EngineCaches.Release();
            // Without compacting: a compacting collection moves old objects, and native code (the UI's Skia renderer)
            // can hold pointers into them; the test run crashed in Skia half a minute after such a trim. Garbage is
            // freed and empty heap regions are decommitted all the same.
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            ProcessMemory.TrimWorkingSet();
            log.LogInformation("Trimmed memory: private {Before} MB → {After} MB (managed heap {Heap} MB) in {Elapsed} ms",
                before, PrivateMegabytes(), GC.GetTotalMemory(false) / (1024 * 1024), watch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.LogWarning(ex, "Could not trim memory");
        }
    }

    private static long PrivateMegabytes()
    {
        using var process = Process.GetCurrentProcess();
        return process.PrivateMemorySize64 / (1024 * 1024);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _timer.Dispose();
        }
    }
}
