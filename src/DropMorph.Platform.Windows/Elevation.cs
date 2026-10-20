// Integrity levels of DropMorph and Explorer, and starting a program with Explorer's rights instead of DropMorph's.
//
// DropMorph runs as administrator when something elevated started it: an installer the user ran "as administrator" starts
// DropMorph with its own token at the end, and the in-app updater inherits that. Explorer then can't drop files on DropMorph
// or hand files to it (see ElevationState). Starting DropMorph again "as the user" goes through Explorer itself: the
// desktop's shell view gives out its Shell.Application object, and ShellExecute on that object runs inside Explorer,
// so the new process gets Explorer's token. This is the documented way back down (no API lowers a running process).

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DropMorph.Core.Platform;

namespace DropMorph.Platform.Windows;

/// <summary>Elevation checks and the de-elevated restart. Every member is safe to call on any OS.</summary>
public static class Elevation
{
    /// <summary>Reads the integrity levels of this process and of Explorer. <see cref="ElevationState.Unknown"/> off Windows.</summary>
    public static ElevationState Probe()
    {
        if (!OperatingSystem.IsWindows())
            return ElevationState.Unknown;
        try
        {
            var process = LevelOfProcess(NativeMethods.GetCurrentProcess(), out var elevated);
            return new ElevationState(process, ShellLevel(), elevated);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return ElevationState.Unknown;
        }
    }

    /// <summary>
    /// Starts <paramref name="executable"/> through Explorer, so it gets Explorer's rights instead of this process's.
    /// Returns false when there is no Explorer desktop to ask (another shell, Explorer restarting) or it refused;
    /// <paramref name="error"/> then says why, in English.
    /// </summary>
    public static bool RelaunchThroughShell(string executable, string arguments, out string? error)
    {
        if (OperatingSystem.IsWindows())
            return RelaunchOnWindows(executable, arguments, out error);
        error = "Not supported on this operating system.";
        return false;
    }

    [SupportedOSPlatform("windows")]
    private static bool RelaunchOnWindows(string executable, string arguments, out string? error)
    {
        // Explorer's COM objects want a single-threaded apartment; a thread of its own keeps the caller's apartment
        // out of it and bounds the wait if Explorer doesn't answer.
        string? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                ShellExecuteFromDesktop(executable, arguments);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Anything Explorer or the COM binder throws: report it, never take DropMorph down from this thread.
                failure = $"{ex.GetType().Name}: {ex.Message}";
            }
        })
        { IsBackground = true, Name = "DropMorph shell relaunch" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15)))
        {
            error = "Explorer did not answer.";
            return false;
        }
        error = failure;
        return failure is null;
    }

    [SupportedOSPlatform("windows")]
    private static void ShellExecuteFromDesktop(string executable, string arguments)
    {
        var shellWindowsType = Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), throwOnError: true)!;
        object? shellWindows = null, desktop = null, browser = null, view = null, background = null;
        try
        {
            shellWindows = Activator.CreateInstance(shellWindowsType)
                           ?? throw new InvalidOperationException("ShellWindows is not available.");
            object location = CSIDL_DESKTOP;
            object? empty = null; // VT_EMPTY
            desktop = ((IShellWindows)shellWindows).FindWindowSW(ref location, ref empty, SWC_DESKTOP, out _, SWFO_NEEDDISPATCH)
                      ?? throw new InvalidOperationException("Explorer's desktop window was not found.");
            var topLevelBrowser = ShellInterop.SID_STopLevelBrowser;
            var shellBrowserId = typeof(ShellInterop.IShellBrowser).GUID;
            browser = ((ShellInterop.IServiceProvider)desktop).QueryService(ref topLevelBrowser, ref shellBrowserId);
            view = ((ShellInterop.IShellBrowser)browser).QueryActiveShellView();
            var dispatchId = IID_IDispatch;
            background = ((IShellView)view).GetItemObject(SVGIO_BACKGROUND, ref dispatchId);
            // IShellFolderViewDual.Application is Explorer's IShellDispatch2: its ShellExecute runs in Explorer.
            dynamic folderView = background;
            dynamic application = folderView.Application;
            try
            {
                application.ShellExecute(executable, arguments, Path.GetDirectoryName(executable) ?? "", "", SW_SHOWNORMAL);
            }
            finally
            {
                Marshal.FinalReleaseComObject((object)application);
            }
        }
        finally
        {
            // Released here, on their apartment's thread, which ends right after. Two of them may be one object.
            foreach (var item in new[] { background, view, browser, desktop, shellWindows }
                         .OfType<object>().Where(Marshal.IsComObject).Distinct(ReferenceEqualityComparer.Instance))
                Marshal.FinalReleaseComObject(item);
        }
    }

    /// <summary>Shows a plain Windows message box (for problems before DropMorph's own windows can open). No-op off Windows.</summary>
    public static void ShowWarning(string text)
    {
        if (OperatingSystem.IsWindows())
            NativeMethods.MessageBox(0, text, "DropMorph",
                NativeMethods.MB_OK | NativeMethods.MB_ICONWARNING | NativeMethods.MB_SETFOREGROUND | NativeMethods.MB_TOPMOST);
    }

    [SupportedOSPlatform("windows")]
    private static IntegrityLevel ShellLevel()
    {
        var desktop = NativeMethods.GetShellWindow();
        if (desktop == 0)
            return IntegrityLevel.Unknown;
        NativeMethods.GetWindowThreadProcessId(desktop, out var pid);
        if (pid == 0)
            return IntegrityLevel.Unknown;
        var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0)
            return IntegrityLevel.Unknown;
        try
        {
            return LevelOfProcess(process, out _);
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    [SupportedOSPlatform("windows")]
    private static unsafe IntegrityLevel LevelOfProcess(nint process, out bool elevated)
    {
        elevated = false;
        if (!NativeMethods.OpenProcessToken(process, NativeMethods.TOKEN_QUERY, out var token))
            return IntegrityLevel.Unknown;
        try
        {
            uint isElevated = 0;
            if (NativeMethods.GetTokenInformation(token, NativeMethods.TokenElevation, &isElevated, sizeof(uint), out _))
                elevated = isElevated != 0;

            // TOKEN_MANDATORY_LABEL: a SID_AND_ATTRIBUTES whose SID points into the same buffer.
            NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, null, 0, out var length);
            if (length == 0)
                return IntegrityLevel.Unknown;
            var buffer = stackalloc byte[(int)length];
            if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, buffer, length, out _))
                return IntegrityLevel.Unknown;
            var sid = *(nint*)buffer;
            var count = *(byte*)NativeMethods.GetSidSubAuthorityCount(sid);
            if (count == 0)
                return IntegrityLevel.Unknown;
            var rid = *(uint*)NativeMethods.GetSidSubAuthority(sid, (uint)(count - 1));
            return ElevationState.FromRid(rid);
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    private const int CSIDL_DESKTOP = 0;
    private const int SWC_DESKTOP = 8;
    private const int SWFO_NEEDDISPATCH = 1;
    private const uint SVGIO_BACKGROUND = 0;
    private const int SW_SHOWNORMAL = 1;
    private static readonly Guid IID_IDispatch = new("00020400-0000-0000-C000-000000000046");

    // Only the methods DropMorph calls are typed; the slots before them keep the vtable order and are never called.

    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IShellWindows
    {
        void get_Count();
        void Item();
        void _NewEnum();
        void Register();
        void RegisterPending();
        void Revoke();
        void OnNavigate();
        void OnActivated();

        [return: MarshalAs(UnmanagedType.IDispatch)]
        object? FindWindowSW(ref object location, ref object? locationRoot, int windowClass, out int hwnd, int options);
    }

    [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellView
    {
        void GetWindow();
        void ContextSensitiveHelp();
        void TranslateAccelerator();
        void EnableModeless();
        void UIActivate();
        void Refresh();
        void CreateViewWindow();
        void DestroyViewWindow();
        void GetCurrentInfo();
        void AddPropertySheetPages();
        void SaveViewState();
        void SelectItem();

        [return: MarshalAs(UnmanagedType.IUnknown)]
        object GetItemObject(uint item, ref Guid riid);
    }
}
