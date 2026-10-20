// Root settings object persisted to settings.json.

using System.Text.Json;
using System.Text.Json.Serialization;
using DropMorph.Core.Presets;

namespace DropMorph.Core.Settings;

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

/// <summary>What closing the main window does (Settings → General).</summary>
public enum CloseAction
{
    /// <summary>Ask, with "Don't ask again" (which saves the answer).</summary>
    Ask,

    /// <summary>Hide the window; DropMorph keeps running in the tray.</summary>
    Tray,

    /// <summary>Quit DropMorph.</summary>
    Quit,
}

/// <summary>Look and feel.</summary>
public sealed class ThemeSettings
{
    public ThemeMode Mode { get; set; } = ThemeMode.System;

    /// <summary>Accent colour as <c>#RRGGBB</c>.</summary>
    public string Accent { get; set; } = "#0D9488";

    /// <summary>Base corner radius in pixels. Larger = "softer" UI.</summary>
    public double CornerRadius { get; set; } = 16;

    /// <summary>
    /// Disables animations (for low-end PCs or motion sensitivity).
    /// <c>null</c> follows the operating system setting.
    /// </summary>
    public bool? ReduceMotion { get; set; }

    public ThemeSettings Clone() => (ThemeSettings)MemberwiseClone();
}

/// <summary>Geometry and appearance of the donut toolbar.</summary>
public sealed class DonutSettings
{
    /// <summary>Outer radius in logical pixels.</summary>
    public double OuterRadius { get; set; } = 150;

    /// <summary>Inner (hole) radius as a fraction of the outer radius (0.2 - 0.7).</summary>
    public double HoleRatio { get; set; } = 0.42;

    /// <summary>Background opacity of the slices (0.5 - 1).</summary>
    public double Opacity { get; set; } = 0.96;

    public DonutSettings Clone() => (DonutSettings)MemberwiseClone();
}

/// <summary>All user settings except presets and profiles (which have their own files).</summary>
public sealed class AppSettings
{
    /// <summary>Current schema version. Increase when the format changes and add a migration.</summary>
    public const int CurrentSchemaVersion = 29;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary><c>"auto"</c>, <c>"en"</c>, <c>"ko"</c> or <c>"zh-CN"</c>.</summary>
    public string Language { get; set; } = "auto";

    public bool StartWithSystem { get; set; } = true;

    /// <summary>Show the Explorer "Convert with DropMorph" context menu entry.</summary>
    public bool ContextMenuEnabled { get; set; } = true;

    public bool CheckForUpdates { get; set; } = true;

    /// <summary>What closing the main window does. A value from a newer DropMorph reads as <see cref="CloseAction.Ask"/>.</summary>
    [JsonConverter(typeof(LenientEnumJsonConverter<CloseAction>))]
    public CloseAction CloseAction { get; set; } = CloseAction.Ask;

    /// <summary>Record finished conversions in the history (home page). Off: nothing new is recorded.</summary>
    public bool KeepHistory { get; set; } = true;

    /// <summary>
    /// How many finished conversions the history keeps, newest first (<see cref="MinHistoryLimit"/> to
    /// <see cref="MaxHistoryLimit"/>; a value outside is taken as the nearest end). The home page lists the latest
    /// ones and searches all of them.
    /// </summary>
    public int HistoryLimit { get; set; } = 200;

    public const int MinHistoryLimit = 50;
    public const int MaxHistoryLimit = 2000;

    /// <summary><see cref="HistoryLimit"/> within its range.</summary>
    [JsonIgnore]
    public int EffectiveHistoryLimit => Math.Clamp(HistoryLimit, MinHistoryLimit, MaxHistoryLimit);

    /// <summary>Whether the macOS Dock shows DropMorph.</summary>
    [JsonConverter(typeof(IconPreferenceJsonConverter))]
    public bool ShowInDock { get; set; } = true;

    /// <summary>Whether the macOS menu bar shows DropMorph's status icon.</summary>
    [JsonConverter(typeof(IconPreferenceJsonConverter))]
    public bool ShowMenuBarIcon { get; set; } = true;

    /// <summary>Newest version the user was already told about, so the update notice shows once per release.</summary>
    public string? NotifiedUpdateVersion { get; set; }

    /// <summary>Engine package id → the new version the engine update notice was shown for (once per version).</summary>
    public Dictionary<string, string> NotifiedEngineVersions { get; set; } = [];

    /// <summary>Temporarily disable all trigger gestures (tray "Pause").</summary>
    public bool Paused { get; set; }

    public ThemeSettings Theme { get; set; } = new();

    public DonutSettings Donut { get; set; } = new();

    public List<TriggerGesture> Triggers { get; set; } = TriggerGesture.Defaults();

    /// <summary>Process names (without .exe) in which gestures never trigger.</summary>
    public List<string> ExcludedProcesses { get; set; } = [];

    /// <summary>Engine ids in preferred order (see <see cref="Conversion.ConverterCatalog.Priority"/>).</summary>
    public List<string> EnginePriority { get; set; } =
    [
        "magick", "pdfium", "pdfsharp", "rhwp", "unhwp", "markdown", "spreadsheet", "ooxml", "hwpx-writer", "docx-writer",
        "pdf-text", "pdf-slides", "email", "data", "vector", "icns", "font", "cad", "model3d", "geo", "archive", "ebook",
        "subtitle", "wic", "ffmpeg", "pandoc", "ghostscript", "calibre", "libreoffice",
    ];

    /// <summary>Set once the first-run welcome has been shown.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Conversions that succeeded completely, counted until the feedback card has been shown.</summary>
    public int SuccessfulConversions { get; set; }

    /// <summary>Set once the "Is DropMorph helping you?" card (GitHub star / feedback) has been shown.</summary>
    public bool FeedbackPromptShown { get; set; }

    /// <summary>Folders whose new files are converted automatically (Settings → Watch folders).</summary>
    public List<Watching.WatchRule> WatchFolders { get; set; } = [];

    /// <summary>Where presets set to "Default" save (Settings → General).</summary>
    public DefaultOutputSettings DefaultOutput { get; set; } = new();

    /// <summary>
    /// Optional engines the user already has: package id ("ffmpeg", "calibre", …) → the program they picked. Used
    /// instead of DropMorph's download (Settings → Engines).
    /// </summary>
    public Dictionary<string, string> EngineOwnCopies { get; set; } = [];

    /// <summary>
    /// With LibreOffice installed, presentations (PPTX, PPT, ODP) → PDF go through it instead of the built-in route
    /// (HWPX writer and rhwp): slide backgrounds and shape positions come out as in PowerPoint.
    /// </summary>
    public bool LibreOfficeForSlides { get; set; } = true;

    /// <summary>
    /// With LibreOffice and H2Orestart installed, HWP / HWPX → PDF go through it instead of rhwp: a way around a
    /// document rhwp lays out wrong (each engine gets different documents right).
    /// </summary>
    public bool LibreOfficeForHwp { get; set; }

    /// <summary>
    /// With LibreOffice installed, Word documents (DOCX, DOC, ODT, RTF) → PDF go through it instead of the built-in
    /// route: Chinese punctuation and line-start rules, fonts the built-in route lacks.
    /// </summary>
    public bool LibreOfficeForDocuments { get; set; }

    /// <summary>
    /// With LibreOffice installed, spreadsheets (XLSX, XLS, ODS, CSV) → PDF go through it: print areas, page setup and
    /// page breaks as Excel prints them.
    /// </summary>
    public bool LibreOfficeForSheets { get; set; }

    /// <summary>Every conversion keeps the created and modified dates of the original, whatever the preset says.</summary>
    public bool KeepFileDates { get; set; }

    /// <summary>Every geospatial conversion is saved as a ZIP, whatever the preset says (see <see cref="OutputRule.ZipResult"/>).</summary>
    public bool ZipGeoResults { get; set; }

    /// <summary>
    /// Returns <paramref name="preset"/> itself, or a copy with the app-wide output settings applied: it saves to
    /// <see cref="DefaultOutput"/> when the preset's location is <see cref="OutputLocation.Default"/>, and keeps the
    /// original dates when <see cref="KeepFileDates"/> is on and is saved as a ZIP when <see cref="ZipGeoResults"/> is on and the
    /// target is geospatial. Name pattern and conflict handling stay the preset's.
    /// </summary>
    public Preset WithDefaultOutput(Preset preset)
    {
        var location = preset.Output.Location == OutputLocation.Default;
        var dates = KeepFileDates && !preset.Output.KeepDates;
        var zip = ZipGeoResults && !preset.Output.ZipResult && Formats.FormatRegistry.FindById(preset.TargetFormat)?.Category == Formats.FormatCategory.Geo;
        if (!location && !dates && !zip)
            return preset;
        var copy = preset.Clone();
        if (location)
        {
            copy.Output.Location = DefaultOutput.Location == OutputLocation.Default ? OutputLocation.SameFolder : DefaultOutput.Location;
            copy.Output.SubfolderName = DefaultOutput.SubfolderName;
            copy.Output.CustomFolder = DefaultOutput.CustomFolder;
        }
        if (dates)
            copy.Output.KeepDates = true;
        if (zip)
            copy.Output.ZipResult = true;
        return copy;
    }
}

/// <summary>Reads unexpected icon preference values as enabled while preserving valid booleans.</summary>
internal sealed class IconPreferenceJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
            return reader.GetBoolean();

        reader.Skip();
        return true;
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}

/// <summary>The save location presets set to "Default" use. Starts as "next to the source file", as before 1.4.</summary>
public sealed class DefaultOutputSettings
{
    /// <summary><see cref="OutputLocation.SameFolder"/>, <see cref="OutputLocation.Subfolder"/> or <see cref="OutputLocation.CustomFolder"/>.</summary>
    public OutputLocation Location { get; set; } = OutputLocation.SameFolder;

    public string SubfolderName { get; set; } = "converted";

    public string CustomFolder { get; set; } = "";
}
