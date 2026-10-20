// State of the interactive crop overlay.

using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DropMorph.App.Services;
using DropMorph.Core.Conversion;
using DropMorph.Core.History;
using DropMorph.Core.Localization;
using DropMorph.Core.Presets;
using DropMorph.Core.Settings;
using Microsoft.Extensions.Logging;

namespace DropMorph.App.ViewModels;

public sealed partial class CropOverlayViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyList<string> _files;
    private readonly Preset _preset;
    private readonly ILocalizer _loc;
    private readonly UserDataStore _store;
    private readonly ILogger? _log;

    private readonly List<string> _processedSources = [];
    private readonly List<string> _outputs = [];
    private readonly List<HistoryFailure> _failures = [];
    private bool _historyRecorded;

    // Set when the window closes. A crop or preview load still running then finishes without touching the closed
    // view (its bitmap disposed, no next file) and records the history with its output.
    private bool _closed;

    [ObservableProperty] private int _currentIndex;
    [ObservableProperty] private string _currentFileName = "";
    [ObservableProperty] private string _counterText = "";
    [ObservableProperty] private bool _hasMultipleFiles;
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private int _originalWidth;
    [ObservableProperty] private int _originalHeight;
    [ObservableProperty] private string _dimensionsText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _aspectRatio;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAspectFree))]
    [NotifyPropertyChangedFor(nameof(IsAspectOriginal))]
    [NotifyPropertyChangedFor(nameof(IsAspectSquare))]
    [NotifyPropertyChangedFor(nameof(IsAspect43))]
    [NotifyPropertyChangedFor(nameof(IsAspect169))]
    private string _selectedAspect = "free";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusNotice))]
    private string _statusNotice = "";

    public bool HasStatusNotice => !string.IsNullOrWhiteSpace(StatusNotice);

    /// <summary>
    /// All files are done and some failed or were skipped: the overlay stays open on <see cref="Summary"/> and
    /// <see cref="Problems"/> until the user closes it, so a problem with the last (or only) file is seen too.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    private bool _isFinished;

    public bool IsEditing => !IsFinished;

    [ObservableProperty] private string _summary = "";

    /// <summary>"file: reason" for each file that failed or was skipped.</summary>
    public ObservableCollection<string> Problems { get; } = [];

    public bool IsAspectFree => SelectedAspect == "free";
    public bool IsAspectOriginal => SelectedAspect == "original";
    public bool IsAspectSquare => SelectedAspect == "1:1";
    public bool IsAspect43 => SelectedAspect == "4:3";
    public bool IsAspect169 => SelectedAspect == "16:9";

    public event Action? CloseRequested;
    public event Action? ResetRequested;
    public event Action<double>? AspectRatioChanged;

    public Rect CurrentCropRect { get; set; } = new(0.05, 0.05, 0.9, 0.9);

    public CropOverlayViewModel(IReadOnlyList<string> files, Preset preset, ILocalizer loc, UserDataStore store, ILogger? log = null)
    {
        _files = files;
        _preset = preset;
        _loc = loc;
        _store = store;
        _log = log;
        _hasMultipleFiles = files.Count > 1;

        _ = LoadFileAsync(0);
    }

    public async Task LoadFileAsync(int index)
    {
        if (index < 0 || index >= _files.Count)
        {
            FinishAndClose();
            return;
        }

        IsBusy = true;
        CurrentIndex = index;
        var path = _files[index];
        CurrentFileName = Path.GetFileName(path);
        CounterText = _loc.Format("crop.page_counter", index + 1, _files.Count);

        try
        {
            var (bitmap, origW, origH) = await CropService.LoadPreviewAsync(path);
            if (_closed)
            {
                bitmap.Dispose();
                RecordHistoryOnce();
                return;
            }
            var old = PreviewImage;
            PreviewImage = bitmap;
            old?.Dispose();

            OriginalWidth = origW;
            OriginalHeight = origH;
            SelectedAspect = "free";
            AspectRatio = 0;
            AspectRatioChanged?.Invoke(0);
            CurrentCropRect = new Rect(0.05, 0.05, 0.9, 0.9);
            UpdateDimensions(CurrentCropRect);
            ResetRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Failed to load preview for {File}", path);
            if (_closed)
            {
                RecordHistoryOnce();
                return;
            }
            _processedSources.Add(path);
            // The animated / multi-frame check throws NotSupportedException: its English message stays in the log.
            var unsupported = ex is NotSupportedException;
            AddFailure(path, unsupported ? "error.unsupported_source" : "error.conversion_failed", unsupported ? null : ex.Message);

            if (index < _files.Count - 1)
                await LoadFileAsync(index + 1);
            else
                FinishAndClose();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void UpdateDimensions(Rect norm)
    {
        CurrentCropRect = norm;
        var pixelW = Math.Max(1, (int)Math.Round(norm.Width * OriginalWidth));
        var pixelH = Math.Max(1, (int)Math.Round(norm.Height * OriginalHeight));
        DimensionsText = $"{pixelW} × {pixelH} px";
    }

    [RelayCommand]
    public async Task CropAndSaveAsync()
    {
        if (IsBusy || IsFinished || _closed || CurrentIndex >= _files.Count)
            return;

        IsBusy = true;
        var path = _files[CurrentIndex];
        _processedSources.Add(path);

        var x = (int)Math.Round(CurrentCropRect.X * OriginalWidth);
        var y = (int)Math.Round(CurrentCropRect.Y * OriginalHeight);
        var w = (int)Math.Round(CurrentCropRect.Width * OriginalWidth);
        var h = (int)Math.Round(CurrentCropRect.Height * OriginalHeight);

        try
        {
            var result = await CropService.CropFileAsync(path, x, y, w, h, _preset, _store, _loc, _log);
            if (result.Status == CropStatus.Success && result.OutputPath is not null)
                _outputs.Add(result.OutputPath);
            else if (result.Status == CropStatus.SkippedExists)
                AddProblem(path, "toast.skipped"); // as in the job queue: skipped counts as done, not as a failure
            else
                AddFailure(path, result.ErrorKey ?? "error.conversion_failed", result.ErrorDetail);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Failed to crop {File}", path);
            AddFailure(path, "error.conversion_failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        if (_closed)
        {
            RecordHistoryOnce();
            return;
        }
        if (CurrentIndex < _files.Count - 1)
        {
            await LoadFileAsync(CurrentIndex + 1);
        }
        else
        {
            FinishAndClose();
        }
    }

    [RelayCommand]
    public async Task SkipAsync()
    {
        if (IsBusy || IsFinished || _closed)
            return;

        if (CurrentIndex < _files.Count - 1)
            await LoadFileAsync(CurrentIndex + 1);
        else
            FinishAndClose();
    }

    [RelayCommand]
    public void Reset()
    {
        SelectedAspect = "free";
        AspectRatio = 0;
        AspectRatioChanged?.Invoke(0);
        ResetRequested?.Invoke();
    }

    /// <summary>Cancel, or Close once finished. A crop still saving finishes and goes into the history.</summary>
    [RelayCommand]
    public void Cancel()
    {
        Shutdown();
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    public void SetAspect(string mode)
    {
        SelectedAspect = mode;
        double ratio = mode switch
        {
            "free" => 0,
            "original" => OriginalHeight > 0 ? (double)OriginalWidth / OriginalHeight : 0,
            "1:1" => 1.0,
            "4:3" => 4.0 / 3.0,
            "16:9" => 16.0 / 9.0,
            _ => 0,
        };
        AspectRatio = ratio;
        AspectRatioChanged?.Invoke(ratio);
    }

    private void AddFailure(string path, string errorKey, string? detail)
    {
        _failures.Add(new HistoryFailure { Source = path, ErrorKey = errorKey, ErrorDetail = detail });
        AddProblem(path, errorKey);
    }

    private void AddProblem(string path, string key)
    {
        var line = $"{Path.GetFileName(path)}: {_loc[key]}";
        Problems.Add(line);
        StatusNotice = line;
    }

    /// <summary>Closes after the last file, or stays open on the summary when a file failed or was skipped.</summary>
    private void FinishAndClose()
    {
        RecordHistoryOnce();
        if (Problems.Count == 0)
        {
            CloseRequested?.Invoke();
            return;
        }
        Summary = _failures.Count > 0
            ? _loc.Format("toast.done_errors", _failures.Count, _processedSources.Count)
            : _loc["toast.done"];
        StatusNotice = "";
        HasMultipleFiles = false;
        var old = PreviewImage;
        PreviewImage = null;
        old?.Dispose();
        IsFinished = true;
    }

    private void Shutdown()
    {
        _closed = true;
        if (!IsBusy)
            RecordHistoryOnce();
    }

    private void RecordHistoryOnce()
    {
        if (_historyRecorded || !_store.Settings.KeepHistory || _processedSources.Count == 0)
            return;

        _historyRecorded = true;
        var state = _failures.Count == 0
            ? JobState.Completed
            : (_outputs.Count > 0 ? JobState.CompletedWithErrors : JobState.Failed);

        _store.AddHistory(new HistoryEntry
        {
            JobId = Guid.NewGuid().ToString("N")[..12],
            FinishedAt = DateTime.Now,
            PresetId = _preset.Id,
            PresetName = _loc.DisplayName(_preset),
            State = state,
            Sources = [.. _processedSources],
            Outputs = [.. _outputs],
            Failures = [.. _failures],
            // No Retry on Home: it would run the preset through ConversionService, which doesn't run tools.
            Preset = null,
        });
    }

    public void Dispose()
    {
        Shutdown();
        PreviewImage?.Dispose();
        PreviewImage = null;
    }
}
