// Opens windows and dialogs: the main (settings) window, the preset popup, confirmations and toasts.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using DropMorph.App.ViewModels;
using DropMorph.App.Views;
using DropMorph.Core.Localization;
using DropMorph.Core.Presets;
using DropMorph.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DropMorph.App.Services;

public sealed class WindowService(ILocalizer loc, IServiceProvider services, MemoryTrimmer trimmer)
{
    private MainWindow? _main;
    private MainWindowPlacement? _mainPlacement;
    private ToastWindow? _toast;
    private EngineSetupWindow? _engineSetup;
    private UpdateNoticeWindow? _updateNotice;
    private EngineUpdateNoticeWindow? _engineNotice;
    private FeedbackNoticeWindow? _feedbackNotice;
    private FormatRequestNoticeWindow? _formatRequestNotice;

    internal FormatRequestNoticeWindow? FormatRequestNotice => _formatRequestNotice;

    private CropOverlayWindow? _cropOverlay;

    public MainWindow? Main => _main;

    /// <summary>Opens the interactive crop overlay for the files.</summary>
    public void ShowCropOverlay(IReadOnlyList<string> files, Preset preset, PixelPoint? near = null)
    {
        if (files.Count == 0)
            return;
        _cropOverlay?.Close();
        var store = services.GetRequiredService<UserDataStore>();
        var log = services.GetService<ILogger<CropOverlayViewModel>>();
        var vm = new CropOverlayViewModel(files, preset, loc, store, log);
        var win = new CropOverlayWindow { DataContext = vm };
        win.PositionOnScreen(near);
        win.Closed += (_, _) =>
        {
            if (_cropOverlay == win)
                _cropOverlay = null;
        };
        _cropOverlay = win;
        win.Show();
        win.Activate();
    }

    /// <summary>
    /// Shows (or brings to front) the main window, optionally on a page. Going to the tray closes it
    /// (<see cref="MainWindow.HideToTray"/>): a new one opens where and on the page the last one was.
    /// </summary>
    public void ShowMain(string? page = null)
    {
        Motion.Refresh();
        if (_main is null)
        {
            var model = ActivatorUtilities.CreateInstance<MainWindowViewModel>(services);
            var main = new MainWindow { DataContext = model };
            if (_mainPlacement is { } placement)
            {
                placement.ApplyTo(main);
                if (page is null)
                    model.Navigate(placement.Page);
            }
            main.Closing += (_, _) =>
            {
                // While it is still on screen: the release closes it hidden, when its position may no longer be known.
                if (main.IsVisible)
                    _mainPlacement = MainWindowPlacement.Of(main, model, _mainPlacement);
            };
            main.Closed += (_, _) =>
            {
                if (_main == main)
                    _main = null;
                model.Dispose();
                trimmer.Request();
            };
            _main = main;
        }
        if (page is not null && _main.DataContext is MainWindowViewModel vm)
            vm.Navigate(page);

        if (!_main.IsVisible)
            _main.Show();
        if (_main.WindowState == WindowState.Minimized)
            _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    /// <summary>Opens the optional-engine choice (first run); a second call brings the open window to front.</summary>
    /// <summary>
    /// Shows the engine choice. With <paramref name="install"/> (packages picked in the installer) the downloads start
    /// right away, and nothing opens when all of them are installed already.
    /// </summary>
    public void ShowEngineSetup(IReadOnlyCollection<string>? install = null)
    {
        if (_engineSetup is not null)
        {
            if (install is not null && _engineSetup.DataContext is EngineSetupViewModel open)
                open.InstallNow(install);
            _engineSetup.Activate();
            return;
        }
        var vm = ActivatorUtilities.CreateInstance<EngineSetupViewModel>(services);
        if (install is not null && !vm.InstallNow(install))
            return;
        _engineSetup = new EngineSetupWindow { DataContext = vm };
        _engineSetup.Closed += (_, _) => _engineSetup = null;
        _engineSetup.Show();
    }

    /// <summary>Creates the toast window once; it shows and hides itself with the job list.</summary>
    public void EnsureToast()
    {
        _toast ??= new ToastWindow { DataContext = services.GetRequiredService<ConversionService>() };
    }

    /// <summary>Shows the "new version available" card in the bottom-right corner (above a visible toast).</summary>
    public void ShowUpdateNotice(string version)
    {
        _updateNotice?.Close();
        var notice = new UpdateNoticeWindow(services.GetRequiredService<UpdateService>(), loc, loc.Format("update.notice_title", version))
        {
            BottomOffset = _toast is { IsVisible: true } toast
                ? (int)Math.Ceiling(toast.Bounds.Height * (toast.Screens.Primary?.Scaling ?? 1))
                : 0,
        };
        notice.Closed += (_, _) =>
        {
            if (_updateNotice == notice)
                _updateNotice = null;
        };
        _updateNotice = notice;
        notice.Show();
    }

    /// <summary>
    /// Says that engines the user downloaded have a newer version. "Update" queues them (one after another) and opens
    /// Settings → Engines to show the progress.
    /// </summary>
    public void ShowEngineUpdateNotice(IReadOnlyList<EnginePackageState> outdated)
    {
        _engineNotice?.Close();
        var downloads = services.GetRequiredService<EngineDownloadService>();
        var caption = string.Join(" · ", outdated.Select(p => $"{p.Name.Split(" (")[0]} {p.Version}"));
        var notice = new EngineUpdateNoticeWindow(caption, () =>
        {
            foreach (var package in outdated)
                _ = downloads.InstallAsync(package);
            ShowMain("engines");
        })
        {
            BottomOffset = _updateNotice is { IsVisible: true } update
                ? (int)Math.Ceiling(update.Bounds.Height * (update.Screens.Primary?.Scaling ?? 1))
                : 0,
        };
        notice.Closed += (_, _) =>
        {
            if (_engineNotice == notice)
                _engineNotice = null;
        };
        _engineNotice = notice;
        notice.Show();
    }

    /// <summary>Shows the one-time "Is DropMorph helping you?" card in the bottom-right corner.</summary>
    public void ShowFeedbackNotice()
    {
        if (_feedbackNotice is not null)
            return;
        _feedbackNotice = new FeedbackNoticeWindow();
        _feedbackNotice.Closed += (_, _) => _feedbackNotice = null;
        _feedbackNotice.Show();
    }

    /// <summary>Shows the "DropMorph is running as administrator" card in the bottom-right corner (once per run).</summary>
    public void ShowElevationNotice()
    {
        if (_elevationNoticeShown)
            return;
        _elevationNoticeShown = true;
        new ElevationNoticeWindow(services.GetRequiredService<ElevationService>()).Show();
    }

    private bool _elevationNoticeShown;

    /// <summary>
    /// Shows the "DropMorph can't convert .ncm files yet" card with "Request this format", replacing an earlier one.
    /// </summary>
    public void ShowFormatRequestNotice(IReadOnlyList<string> extensions)
    {
        _formatRequestNotice?.Close();
        var notice = new FormatRequestNoticeWindow(extensions, loc.Format("format_request.notice_title", FormatRequest.Label(extensions)));
        notice.Closed += (_, _) =>
        {
            if (_formatRequestNotice == notice)
                _formatRequestNotice = null;
        };
        _formatRequestNotice = notice;
        notice.Show();
    }

    /// <summary>Opens the preset popup. Returns true when the user saved (the preset object was updated).</summary>
    public async Task<bool> EditPresetAsync(Preset preset, Window? owner)
    {
        // A group converts nothing: it gets its own editor (name and the presets it fans out).
        if (preset.IsGroup)
            return await EditGroupAsync(preset, owner);
        var dialog = new PresetEditorWindow
        {
            DataContext = new PresetEditorViewModel(preset, loc),
            Topmost = owner?.Topmost ?? false,
        };
        owner ??= _main ?? MainWindowOrNull();
        if (owner is null || !owner.IsVisible)
        {
            // No visible owner (e.g. the donut was opened from the tray): show as a normal window.
            var closed = new TaskCompletionSource();
            dialog.Closed += (_, _) => closed.TrySetResult();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Show();
            await closed.Task;
            return dialog.Saved;
        }
        return await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>
    /// Opens the group editor. Returns true when the user saved (the group was updated) or deleted it (it was removed
    /// from the presets and every donut). <paramref name="isNew"/>: not in the store yet, so it can't be deleted.
    /// </summary>
    public async Task<bool> EditGroupAsync(Preset group, Window? owner, bool isNew = false)
    {
        owner ??= _main ?? MainWindowOrNull();
        var visibleOwner = owner is { IsVisible: true } ? owner : null;
        var dialog = new GroupEditorWindow
        {
            DataContext = new GroupEditorViewModel(group, services.GetRequiredService<UserDataStore>(), loc, isNew),
            Topmost = owner?.Topmost ?? false,
            Confirm = (editor, message) => ConfirmAsync(editor, message, destructive: true),
        };
        if (visibleOwner is null)
        {
            var closed = new TaskCompletionSource();
            dialog.Closed += (_, _) => closed.TrySetResult();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Show();
            await closed.Task;
            return dialog.Changed;
        }
        return await dialog.ShowDialog<bool>(visibleOwner);
    }

    /// <summary>Simple yes/no confirmation.</summary>
    public Task<bool> ConfirmAsync(Window owner, string message, bool destructive = false) =>
        ShowMessageAsync(owner, message, destructive, withCancel: true);

    /// <summary>A message with only an OK button. Without a visible owner it shows on its own, centred on the screen.</summary>
    public Task MessageAsync(Window? owner, string message) => ShowMessageAsync(owner, message, destructive: false, withCancel: false);

    /// <summary>The in-app update failed: say so, then open the website to download the new version there.</summary>
    public async Task ShowUpdateFailedAsync()
    {
        _updateNotice?.Close();
        await MessageAsync(_main is { IsVisible: true } main ? main : null, loc["update.failed_alert"]);
        services.GetRequiredService<UpdateService>().OpenDownloadPage();
    }

    private async Task<bool> ShowMessageAsync(Window? owner, string message, bool destructive, bool withCancel)
    {
        var dialog = new Window
        {
            Title = loc["app.name"],
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        Motion.Track(dialog);
        var yes = new Button { Content = destructive ? loc["common.delete"] : loc["common.ok"], IsDefault = true };
        yes.Classes.Add("accent");
        var no = new Button { Content = loc["common.cancel"], IsCancel = true };
        var answer = false;
        yes.Click += (_, _) => { answer = true; dialog.Close(true); };
        no.Click += (_, _) => dialog.Close(false);
        no.IsVisible = withCancel;
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { no, yes },
                },
            },
        };
        if (owner is { IsVisible: true })
            return await dialog.ShowDialog<bool>(owner);

        // From the tray or the bottom-right notice there is no window to sit on: a normal window on top instead.
        var closed = new TaskCompletionSource();
        dialog.Closed += (_, _) => closed.TrySetResult();
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowInTaskbar = true;
        dialog.Topmost = true;
        dialog.Show();
        dialog.Activate();
        await closed.Task;
        return answer;
    }

    private static Window? MainWindowOrNull() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    /// <summary>Where the main window was and which page it showed, for the next one.</summary>
    private sealed record MainWindowPlacement(PixelPoint? Position, Size? Size, bool Maximized, string Page)
    {
        /// <summary>The window's placement. Maximized or minimized, it keeps the position and size it had before.</summary>
        public static MainWindowPlacement Of(MainWindow window, MainWindowViewModel vm, MainWindowPlacement? last) =>
            window.WindowState switch
            {
                WindowState.Normal => new(window.Position, window.ClientSize, false, vm.SelectedItem.Key),
                WindowState.Maximized => (last ?? new(null, null, false, "")) with { Maximized = true, Page = vm.SelectedItem.Key },
                _ => (last ?? new(null, null, false, "")) with { Page = vm.SelectedItem.Key },
            };

        public void ApplyTo(MainWindow window)
        {
            // Only on a screen that is still there (a monitor may have been unplugged since).
            if (Position is { } position && window.Screens.ScreenFromPoint(position) is not null)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = position;
            }
            if (Size is { } size)
            {
                window.Width = size.Width;
                window.Height = size.Height;
            }
            if (Maximized)
                window.WindowState = WindowState.Maximized;
        }
    }
}
