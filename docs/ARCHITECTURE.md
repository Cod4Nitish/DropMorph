# Architecture

```
            ┌──────────────────────────── DropMorph.App (Avalonia) ─────────────────────────────┐
 global     │ TriggerService ──► GestureDetector ──► RadialController ──► RadialWindow/DonutMenu │
 mouse/keys │   (SharpHook)        (pure state         (drag / click       (transparent,          │
 ─────────► │                       machine)            mode)               topmost window)       │
            │                                              │ drop / click                         │
 Explorer   │ SingleInstance (pipe) ── --convert files ──►│                                      │
 context    │ Home drop zone ─────────────────────────────┘                                      │
 menu       │                                              ▼                                      │
            │                                    ConversionService ──► ToastWindow (progress)     │
            │ MainWindow (settings pages) ◄──► UserDataStore (JSON in %APPDATA%\DropMorph)            │
            └──────────────────────────────────────────────┬──────────────────────────────────────┘
                                                           ▼
            ┌──────────────────────────── DropMorph.Core ───────────────────────────────────────────┐
            │ JobQueue ──► RoutePlanner (Dijkstra over converter edges) ──► IConverter steps    │
            │ FormatRegistry · Preset · OutputPathResolver · ToolbarProfile · ProfileSelector   │
            └──────────────────────────────────────────────┬──────────────────────────────────────┘
                                                           ▼
            ┌──────────────────────────── DropMorph.Engines ────────────────────────────────────────┐
            │ Built in: Magick (images, RAW) · PdfSharp · Pdfium · PdfPig · Svg.Skia · ICNS      │
            │ HwpxWriter + DocxWriter over DocumentReaders (DOCX, XLSX, XLS, ODS, PPTX,          │
            │   PDF, HTML, EPUB, MOBI, FB2, HWPX, Markdown) · Spreadsheets · OOXML variants      │
            │   · E-mail · Data · E-books · CAD · Fonts · Subtitles · 3D models                  │
            │ Bundled: rhwp (HWP→PDF, HWP↔HWPX) · 7-Zip (archives) · Unhwp · Markdig             │
            │ Optional: FFmpeg · LibreOffice + H2Orestart · calibre · Ghostscript · Pandoc       │
            └────────────────────────────────────────────────────────────────────────────────────┘
```

## Flow of a conversion

1. **Trigger** – `TriggerService` receives global input from SharpHook and feeds `GestureDetector`.
   A drag gesture (modifier + drag past the threshold) opens the donut *before* files are known.
2. **Donut** – `RadialWindow` is transparent, topmost and not activated, so Explorer's drag continues.
   The first `DragEnter` delivers the file list; `RadialViewModel` picks a `ToolbarProfile` by extension
   (`ProfileSelector`: the first normal profile that has every file's extension; for a mix of types the first
   mixed-selection profile whose checked extensions cover the files, else the one with nothing checked) and asks
   `PresetAvailability` which presets can run (disabled slices get a reason).
3. **Drop** – the slice under the cursor is a preset. `ConversionService.Start` enqueues a `ConversionJob`.
   The drop is always reported as *Copy* so Explorer never deletes the source files.
4. **Plan** – for every file, `RoutePlanner` finds the cheapest chain of converter edges from the source format to
   the target (at most 3 hops). Engine priority (Settings → Engines) breaks ties. Some edges are limited: tracing
   only starts from raster images (`ImageSourcesOnly`), and PDF → PPTX picture slides are only ever a route of their
   own (`DirectOnly`), never a step of a longer one.
5. **Run** – `JobQueue` runs steps with per-engine concurrency limits. Intermediate files live in a temp folder;
   final outputs are named by `OutputPathResolver` (pattern, folder, conflict policy; the source is never overwritten).
   "Merge into one PDF" converts each file to PDF and merges with PDFsharp.
   "Compress into one archive" packs the dropped files, unconverted, with the archive engine (`IFileCombiner`).
6. **Report** – progress and results appear in the toast window and in Home → Recent conversions.

## Donut control

`Controls/DonutMenu.cs` draws the ring itself (no child controls) for speed. `DonutGeometry.cs` holds the pure math
(slice angles, hit testing) and is unit tested. The same control is the live toolbar and, with `IsEditMode`, the
editor on the *Donut toolbar* settings page (drag to reorder, ✎ / right-click to edit).
Animations are small springs advanced per frame; `Motion.Enabled == false` (*Reduce animations*) snaps them.

## Settings and data

`UserDataStore` owns `settings.json`, `presets.json`, `profiles.json` and `history.json`
(System.Text.Json source generation, atomic writes, broken files are kept as `*.broken`).
Bump `AppSettings.CurrentSchemaVersion` and add a step to `SettingsMigrations` when the format changes.

## Localization and theming

- `LocalizationService` loads `Assets/i18n/<lang>.json` into `Application.Resources`; XAML uses
  `{DynamicResource key}`, so switching language updates the UI live.
- `ThemeService` writes the accent colour, corner radii, fonts and Fluent's `SystemAccentColor*` into
  application resources. Light/dark surface colours are theme dictionaries in `Styles/Soft.axaml`; a user's
  `theme.json` (`CustomTheme`, see [CUSTOM-THEMES.md](CUSTOM-THEMES.md)) overrides them with theme dictionaries in
  application resources, which are searched first.

## Command line and watch folders

- `DropMorph.Cli` (`dropmorph-cli.exe`, run as `dropmorph` through `cli\dropmorph.cmd`, which Setup can put on PATH) composes the same
  pipeline as the app without any UI (`CliHost`: `UserDataStore`, `EngineRegistry`, `ConverterCatalog`, `JobQueue`)
  and reads the app's presets and engine order. It is published into the app folder, so both share one runtime and
  the bundled engines; its name can't be `dropmorph.exe` next to `DropMorph.exe` on a case-insensitive file system.
- `DropMorph.Core/Watching/FolderWatcher` watches one `WatchRule` with a `FileSystemWatcher` and converts a file once it
  has kept its size and time for a moment and opens without sharing (downloads and copies in progress are skipped).
  Temporary files, unknown formats and the output and originals folders are ignored, so outputs never loop back.
  The tray app runs one per rule (`WatchFolderService`, Settings → Watch folders, conversions in the toast and
  history); `dropmorph watch` runs one on its own.

## Platform code

Everything OS-specific goes through `IPlatformServices` (`DropMorph.Core/Platform`). Windows lives in
`DropMorph.Platform.Windows`; macOS will get its own project. Global input uses SharpHook on both.

On macOS, files reach DropMorph from file managers other than Finder too (issue #160). The "Convert with DropMorph" service
(`MacOSServicesProvider`) and the donut's drags (`DraggedFiles`) read file URLs first, then the older
`NSFilenamesPboardType` array of paths (`PasteboardPaths`); the service also takes plain-text lines that are absolute
paths. The donut registers those types with AppKit next to Avalonia's own, so such a drag reaches it, and a drag or
service request without readable files is logged with the types it offered. File promises (`NSFilePromiseProvider`)
can't be received through Avalonia. A click-mode donut opened for a later launch or the service forgives one loss of
focus within a second of opening (`ActivationGrace`): DropMorph becoming the active app can take focus away from it.

## Explorer integration

Settings → General → "Show “Convert with DropMorph” in the Explorer context menu" (`AppSettings.ContextMenuEnabled`) is
applied by `WindowsPlatformServices.SetContextMenu` at start-up and whenever settings change (installed builds only):

- **Classic verb** `HKCU\Software\Classes\*\shell\DropMorph` (`DropMorph.exe --convert "%1"`, one process per file; the running
  instance collects them through `SingleInstance`) and a **Send To** shortcut. Per user, no admin rights. On Windows 10
  this is the entry; on Windows 11 it sits under "Show more options".
- **Windows 11 top-level entry** (optional): an `IExplorerCommand` in `DropMorphExplorerMenu.dll`
  (`src/DropMorph.ExplorerMenu`, plain C, no C runtime) declared by the sparse package `DropMorphExplorerMenu.msix`
  (`ExplorerMenuPackage.CreateManifest`: `desktop4:FileExplorerContextMenus`, `desktop5:ItemType Type="*"`,
  `com:SurrogateServer`, `uap10:AllowExternalContent`, `AppListEntry="none"`). Both files ship next to DropMorph.exe and the
  package's external location is the install folder (`C:\Program Files\DropMorph` by default).
  - The package is **unsigned** (publisher contains `OID.2.25.311729368913984317654407730594956997722=1`; DropMorph has no
    code-signing certificate). Windows only registers an unsigned package with executable content **with
    administrator rights** (it installs it for all users); a per-user attempt fails with 0x80073D2B "an unsigned
    package cannot include Executable activations". Setup (`installer/DropMorph.iss`) has those rights anyway and
    registers it on Windows 11 x64 while its option is ticked. Later the General page offers "Add to the main menu",
    which runs `Add-AppxPackage -Path … -ExternalLocation … -AllowUnsigned` in Windows PowerShell through one UAC
    prompt (`ExplorerMenuRegistration`). A signed package would register per user without a prompt.
  - The manifest version (`ExplorerMenuPackage.ManifestVersion`) is independent of the app version, so app updates
    don't need a new prompt: the DLL is loaded from the install folder and updated with the app. Bump it only when the
    manifest changes; the page then offers to add the entry again ("Outdated").
  - The DLL shows the entry only for files (not folders or items inside ZIP folders) and only while
    `%APPDATA%\DropMorph\explorer-menu.txt` exists. The app writes the title in the UI language there while the setting is
    on and deletes it when the setting is off, so switching the setting takes effect without re-registering. While the
    package is registered the classic verb is removed (Windows lists packaged commands under "Show more options" too).
  - Invoke starts `DropMorph.exe --convert "<path>"…` next to the DLL and returns at once. Selections longer than a
    command line go through `%TEMP%\DropMorph-convert-*.txt` (`--convert-list`, one path per line), which the app deletes.
  - Updates: Setup asks the running DropMorph to exit (`DropMorph.exe --quit`), then renames a `DropMorphExplorerMenu.dll` that
    Explorer still has loaded (Windows allows renaming a loaded DLL, not replacing or deleting it) and has Windows
    delete the old copy at the next restart, so updating needs no restart. An update keeps the registration as it
    was and refreshes it. Uninstall runs `DropMorph.exe --uninstall-cleanup` (`UninstallCleanup`): it removes the package,
    the title file, the classic verb, the startup entry and downloaded engines; settings stay.
  - Build: `pwsh build/build-explorer-menu.ps1` downloads a pinned Zig (SHA-256 checked) into `build/.cache`, compiles
    the DLL and packs the MSIX with `build/tools/make-explorer-package.cs` (Windows' own packaging API, no SDK).
    Output: `build/.cache/explorer-menu`; CI builds it for the tests, `build/build-installer.ps1` builds it into the
    publish folder.

Debugging: `Get-AppxPackage DropMorph.ExplorerMenu` shows the registration (`InstallLocation` is the external location);
`Get-AppPackageLog -ActivityID <id>` explains a failed deployment. To try a local build, copy
`DropMorphExplorerMenu.dll` + `.msix` next to an installed DropMorph.exe and use the button, or run elevated:
`Add-AppxPackage -Path DropMorphExplorerMenu.msix -ExternalLocation <folder with DropMorph.exe> -AllowUnsigned`. Explorer picks
the entry up for new menus; if not, restart Explorer. To remove it by hand:
`Get-AppxPackage DropMorph.ExplorerMenu | Remove-AppxPackage`.
