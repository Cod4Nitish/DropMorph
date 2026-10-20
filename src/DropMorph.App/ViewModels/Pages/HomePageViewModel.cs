// Home: drop zone (handled in the view) and recent conversions.

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropMorph.App.Services;
using DropMorph.Core.Conversion;
using DropMorph.Core.History;
using DropMorph.Core.Localization;
using DropMorph.Core.Platform;
using DropMorph.Core.Settings;

namespace DropMorph.App.ViewModels.Pages;

public sealed partial class HistoryItemViewModel : ObservableObject
{
    private readonly HistoryEntry _entry;
    private readonly IPlatformServices _platform;
    private readonly ConversionService? _conversions;

    public HistoryItemViewModel(HistoryEntry entry, ILocalizer loc, IPlatformServices platform, ConversionService? conversions = null)
    {
        _entry = entry;
        _platform = platform;
        _conversions = conversions;
        Title = entry.PresetName;
        Subtitle = entry.Sources.Count == 1 ? Path.GetFileName(entry.Sources[0]) : loc.Format("home.files", entry.Sources.Count);
        Time = entry.FinishedAt.Date == DateTime.Today ? entry.FinishedAt.ToString("t") : entry.FinishedAt.ToString("d");
        Failures = entry.Failures.Count > 0
            ? entry.Failures.Select(f => new FailureItem(Path.GetFileName(f.Source), FailureText.Reason(loc, f.ErrorKey, f.ErrorDetail))).ToList()
            : entry.Errors.Select(line => FailureText.FromLegacy(loc, line)).ToList();
        HasErrors = Failures.Count > 0 || entry.State == JobState.Failed;
        FailureSummary = loc.Format("home.failed_files", Failures.Count);
        HasOutputs = entry.Outputs.Any(ConversionService.Exists);
        // Only entries that remember their preset (DropMorph 1.8 and later) and still have a failed file to convert.
        CanRetry = conversions is not null && entry.Preset is not null && entry.Failures.Any(f => ConversionService.Exists(f.Source));
    }

    public string Title { get; }
    public string Subtitle { get; }
    public string Time { get; }
    public bool HasErrors { get; }
    public bool HasOutputs { get; }

    /// <summary>The files that failed and why.</summary>
    public IReadOnlyList<FailureItem> Failures { get; }

    public bool HasFailures => Failures.Count > 0;

    /// <summary>"2 files failed": the button that shows the list.</summary>
    public string FailureSummary { get; }

    public bool CanRetry { get; }

    [ObservableProperty] private bool _showFailures;

    [RelayCommand]
    private void ToggleFailures() => ShowFailures = !ShowFailures;

    /// <summary>Converts the failed files again the way the job ran them; progress shows in the toast.</summary>
    [RelayCommand]
    private void Retry()
    {
        if (_conversions is not null && _entry.Preset is not null)
            _conversions.Retry(_entry.Failures.Select(f => f.Source), _entry.Preset, _entry.WatchRuleId);
    }

    [RelayCommand]
    private void OpenFolder()
    {
        // An output may be a folder ("Extract"): revealing it opens the extracted files.
        var output = _entry.Outputs.FirstOrDefault(ConversionService.Exists);
        if (output is not null)
            _platform.RevealInFileManager(output);
    }
}

public sealed partial class HomePageViewModel : ObservableObject, IDisposable
{
    private readonly UserDataStore _store;
    private readonly ILocalizer _loc;
    private readonly IPlatformServices _platform;
    private readonly Services.Triggers.TriggerService? _triggers;
    private readonly ConversionService? _conversions;
    private readonly ElevationService? _elevation;

    public HomePageViewModel(UserDataStore store, ILocalizer loc, IPlatformServices platform, Services.Triggers.TriggerService? triggers = null,
        ConversionService? conversions = null, ElevationService? elevation = null)
    {
        _store = store;
        _loc = loc;
        _platform = platform;
        _triggers = triggers;
        _conversions = conversions;
        _elevation = elevation;
        if (triggers is not null)
            triggers.StatusChanged += OnTriggerStatusChanged;
        if (elevation is not null)
            elevation.PropertyChanged += OnElevationChanged;
        var drag = store.Settings.Triggers.FirstOrDefault(t => t.Enabled && t.Kind == TriggerKind.Drag);
        DropHint = loc.Format(OperatingSystem.IsMacOS() ? "home.drop_hint_macos" : OperatingSystem.IsLinux() ? "home.drop_hint_linux" : "home.drop_hint", drag is null ? "—" : GestureText.Modifiers(drag.Modifiers));
        store.HistoryChanged += OnHistoryChanged;
        Reload();
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Reload);

    public void Dispose()
    {
        _store.HistoryChanged -= OnHistoryChanged;
        if (_triggers is not null)
            _triggers.StatusChanged -= OnTriggerStatusChanged;
        if (_elevation is not null)
            _elevation.PropertyChanged -= OnElevationChanged;
    }

    private void OnTriggerStatusChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(NeedsAccessibility));

    private void OnElevationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(ElevationRestartFailed));

    /// <summary>
    /// Windows: DropMorph runs as administrator while Explorer doesn't, so drops, the Explorer menu and the shortcut can't
    /// reach it (the warning above the drop zone).
    /// </summary>
    public bool IsElevated => _elevation?.ShellCannotReachDropMorph == true;

    /// <summary>The restart button didn't work: the warning says what to do instead.</summary>
    public bool ElevationRestartFailed => _elevation?.RestartFailed == true;

    [RelayCommand]
    private void RestartWithoutAdministratorRights() => _elevation?.RestartWithoutAdministratorRights();

    /// <summary>macOS: gestures wait for the Accessibility permission (the reminder above the drop zone).</summary>
    public bool NeedsAccessibility => _triggers?.NeedsPermission == true;

    [RelayCommand]
    private void OpenAccessibility()
    {
        if (OperatingSystem.IsMacOS() && _platform is DropMorph.Platform.MacOS.MacOSPlatformServices mac)
            mac.OpenAccessibilitySettings();
    }

    public string DropHint { get; }

    /// <summary>How many conversions the page lists: the newest, or the newest that match the search.</summary>
    internal const int Shown = 40;

    /// <summary>The conversions listed (each looks for its outputs on disk, so only <see cref="Shown"/> of them).</summary>
    public ObservableCollection<HistoryItemViewModel> History { get; } = [];

    /// <summary>
    /// What was typed in the search box: the list shows the conversions whose preset or a source file name contains
    /// it, from the whole history (<see cref="AppSettings.HistoryLimit"/>), not just the ones listed without it.
    /// </summary>
    [ObservableProperty] private string _search = "";

    partial void OnSearchChanged(string value) => Reload();

    /// <summary>No history at all: no search box, and the hint that converted files show up here.</summary>
    [ObservableProperty] private bool _isEmpty;

    /// <summary>A search that matches nothing.</summary>
    [ObservableProperty] private bool _noMatches;

    private void Reload()
    {
        var query = Search.Trim();
        // The entries are picked first, then built: building one looks for its outputs on disk.
        var entries = _store.History
            .Where(entry => query.Length == 0 || Matches(entry, query))
            .Take(Shown)
            .ToList();
        History.Clear();
        foreach (var entry in entries)
            History.Add(new HistoryItemViewModel(entry, _loc, _platform, _conversions));
        IsEmpty = _store.History.Count == 0;
        NoMatches = !IsEmpty && History.Count == 0;
    }

    private static bool Matches(HistoryEntry entry, string query)
    {
        return Contains(entry.PresetName) || entry.Sources.Any(source => Contains(Path.GetFileName(source)));

        bool Contains(string? text) => text?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
    }

    [RelayCommand]
    private void ClearHistory()
    {
        Search = "";
        _store.ClearHistory();
        Reload();
    }
}
