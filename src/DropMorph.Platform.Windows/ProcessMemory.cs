// Hands the pages of this process back to Windows once it is idle in the tray, so Task Manager shows what DropMorph
// really needs instead of what the last conversion touched.

namespace DropMorph.Platform.Windows;

public static class ProcessMemory
{
    /// <summary>
    /// Removes as many pages as possible from the working set (they come back on use). No-op off Windows; false when
    /// Windows refused.
    /// </summary>
    public static bool TrimWorkingSet() =>
        !OperatingSystem.IsWindows() || NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), -1, -1);
}
