using DropMorph.Core.Localization;
using DropMorph.Core.Platform;
using DropMorph.Core.Settings;
using DropMorph.Platform.Linux;
using Microsoft.Extensions.Logging;

namespace DropMorph.App.Services;

public sealed class SystemIntegrationService(IPlatformServices platform, ILocalizer loc, ILogger<SystemIntegrationService> log)
{
    private (bool Enabled, string Executable)? _startup;
    private (bool Enabled, string Executable, string Label)? _menu;
    private bool _shortcutApplied;
    private string? _shortcut;
    private bool _skipLogged;

    public string? Error { get; private set; }
    internal bool RegistrationEnabled { get; init; } = UpdateService.IsReleaseBuild;
    internal string? ExecutablePath { get; init; } = Environment.ProcessPath;

    /// <summary>
    /// A copy started with DROPMORPH_DATA_DIR (<see cref="UserDataStore.DirectoryVariable"/>): its startup entry, context
    /// menu and Explorer menu would replace those of the DropMorph the user installed, so it registers nothing.
    /// </summary>
    internal bool SeparateDataDirectory { get; init; } = UserDataStore.OverrideDirectory is not null;

    public void Apply(AppSettings settings)
    {
        Error = null;
        if (SeparateDataDirectory)
        {
            if (!_skipLogged)
                log.LogInformation("{Variable} is set: startup entry, context menu and Explorer menu are left alone", UserDataStore.DirectoryVariable);
            _skipLogged = true;
            return;
        }
        if (!RegistrationEnabled || ExecutablePath is not { } executable)
            return;
        var errors = new List<string>();
        var startup = (settings.StartWithSystem, executable);
        if (_startup != startup && ApplyOne(() => platform.SetStartWithSystem(startup.StartWithSystem, executable)))
            _startup = startup;
        var menu = (settings.ContextMenuEnabled, executable, Label: loc["general.context_menu_label"]);
        if (_menu != menu && ApplyOne(() => platform.SetContextMenu(menu.ContextMenuEnabled, executable, menu.Label)))
        {
            _menu = menu;
            _shortcutApplied = false;
        }
        if (OperatingSystem.IsLinux() && platform is LinuxPlatformServices linux)
        {
            ApplyOne(() =>
            {
                var shortcut = settings.ContextMenuEnabled && !settings.Paused
                    ? settings.Triggers.FirstOrDefault(g => g.Enabled && g.Kind == TriggerKind.KeyChord) : null;
                var accelerator = shortcut is null ? null : LinuxPlatformServices.ToThunarAccelerator(shortcut);
                if (!_shortcutApplied || _shortcut != accelerator)
                {
                    linux.SetThunarShortcut(accelerator);
                    _shortcut = accelerator;
                    _shortcutApplied = true;
                }
            });
        }
        if (errors.Count > 0)
            Error = loc.Format("general.integration_failed", string.Join(Environment.NewLine, errors));

        bool ApplyOne(Action action)
        {
            try { action(); return true; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                log.LogWarning(ex, "Could not update system integration");
                errors.Add(ex.Message);
                return false;
            }
        }
    }
}
