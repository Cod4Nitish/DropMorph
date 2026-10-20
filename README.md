<p align="center">
  <img src="docs/media/banner.svg" alt="DropMorph: Drag. Drop. Morphed." width="100%">
</p>

<p align="center">
  <b>A free file converter you use by dragging.</b><br>
  Hold <kbd>Ctrl</kbd> (<kbd>Option</kbd> on macOS), drag files and drop them on a format in the donut that opens at your cursor.
</p>

<p align="center">
  <a href="https://dropmorph.vercel.app/"><b>Website</b></a> ·
  <a href="#download"><b>Download</b></a> ·
  <a href="#install">Install</a> ·
  <a href="#screenshots">Screenshots</a> ·
  <a href="#command-line">Command line</a> ·
  <a href="#development">Development</a>
</p>

<p align="center">
  <a href="https://github.com/cod4nitish/DropMorph/actions/workflows/ci.yml"><img src="https://github.com/cod4nitish/DropMorph/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/cod4nitish/DropMorph/releases/latest"><img src="https://img.shields.io/github/v/release/cod4nitish/DropMorph?color=0D9488&label=release" alt="Latest release"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0B6F66" alt="Windows 10 | 11">
  <img src="https://img.shields.io/badge/macOS-14%2B-FF6B35" alt="macOS 14+">
  <img src="https://img.shields.io/badge/Linux-x64%20%7C%20ARM64-FF6B35" alt="Linux x64 | ARM64">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-0D9488" alt="MIT license"></a>
</p>

<p align="center">
  <a href="https://github.com/cod4nitish/DropMorph/stargazers"><img src="https://img.shields.io/github/stars/cod4nitish/DropMorph?style=for-the-badge&logo=github&label=Star%20DropMorph&color=FF6B35" alt="Star DropMorph on GitHub"></a><br>
  <sub>DropMorph is free. If it saves you time, <b>give it a ⭐</b> (top right of this page): it helps the developer and helps others find it.</sub>
</p>

<table>
  <tr>
    <td width="33%" valign="top"><b>Right at your cursor</b><br>No app window to open. The formats appear where you already are, and only the ones that make sense for the files you drag.</td>
    <td width="33%" valign="top"><b>254 formats, no other software</b><br>Images, PDF, Word, Excel, PowerPoint, HWP, e-books, archives, fonts, CAD, 3D models and map data, without Office or LibreOffice.</td>
    <td width="33%" valign="top"><b>Stays on your computer</b><br>Nothing is uploaded. DropMorph works offline and saves the result next to the original.</td>
  </tr>
</table>

## Download

Version 1.12.0. Pick your system and click to download; the buttons link straight to the files.

<table>
  <tr>
    <th width="33%">Windows</th>
    <th width="33%">macOS</th>
    <th width="33%">Linux</th>
  </tr>
  <tr>
    <td valign="top" align="center">
      <a href="https://github.com/cod4nitish/DropMorph/releases/download/v1.12.0/DropMorph-1.12.0-win-Setup.exe"><img src="https://img.shields.io/badge/Download-Windows%20Setup-0D9488?style=for-the-badge&logo=windows&logoColor=white" alt="Download for Windows (Setup)"></a><br>
      <a href="https://github.com/cod4nitish/DropMorph/releases/download/v1.12.0/DropMorph-1.12.0-win-Portable.zip"><img src="https://img.shields.io/badge/Download-Portable%20ZIP-0B6F66?style=for-the-badge&logo=windows&logoColor=white" alt="Download for Windows (portable ZIP)"></a>
      <br><br>
      64-bit Windows 10 (1809 or newer) or Windows 11.<br>
      Setup is recommended; the ZIP runs without installing.
      <br><br>
      <a href="https://dropmorph.vercel.app/install/">Step-by-step guide</a>
    </td>
    <td valign="top" align="center">
      <a href="https://github.com/cod4nitish/DropMorph/releases/download/v1.12.0/DropMorph-1.12.0-osx-arm64.tar.gz"><img src="https://img.shields.io/badge/Download-Apple%20silicon%20(M1%E2%80%93M4)-0D9488?style=for-the-badge&logo=apple&logoColor=white" alt="Download for Mac with Apple silicon (M1 to M4)"></a><br>
      <a href="https://github.com/cod4nitish/DropMorph/releases/download/v1.12.0/DropMorph-1.12.0-osx-x64.tar.gz"><img src="https://img.shields.io/badge/Download-Intel%20Mac-0B6F66?style=for-the-badge&logo=apple&logoColor=white" alt="Download for Intel Mac"></a>
      <br><br>
      macOS 14 Sonoma or newer.<br>
      <b>Which one?</b> Apple menu → <b>About This Mac</b>:<br>
      <b>Chip</b>: Apple M1, M2, M3, M4 → Apple silicon<br>
      <b>Processor</b>: Intel → Intel Mac
      <br><br>
      <a href="docs/MACOS.md">macOS guide</a> · <a href="https://dropmorph.vercel.app/install/mac/">Hinglish guide</a>
    </td>
    <td valign="top" align="center">
      <a href="https://github.com/cod4nitish/DropMorph/releases/download/v1.12.0/DropMorph-1.12.0-linux-x64.tar.gz"><img src="https://img.shields.io/badge/Download-Linux%20x64-0D9488?style=for-the-badge&logo=linux&logoColor=white" alt="Download for Linux x64"></a><br>
      <a href="https://github.com/cod4nitish/DropMorph/releases/download/v1.12.0/DropMorph-1.12.0-linux-arm64.tar.gz"><img src="https://img.shields.io/badge/Download-Linux%20ARM64-0B6F66?style=for-the-badge&logo=linux&logoColor=white" alt="Download for Linux ARM64"></a>
      <br><br>
      Ubuntu 22.04+ or any distribution with glibc 2.35+, and Python 3.<br>
      <code>uname -m</code> prints <code>x86_64</code> → x64, <code>aarch64</code> → ARM64.
      <br><br>
      <a href="docs/LINUX.md">Linux guide</a> · <a href="https://dropmorph.vercel.app/install/linux/">Hinglish guide</a>
    </td>
  </tr>
</table>

**Or install with one command.** It downloads the latest release for your computer (on a Mac it picks Apple silicon or
Intel for you), checks its SHA-256 checksum and installs it.

macOS and Linux, in Terminal:

```sh
curl -fsSL https://dropmorph.vercel.app/install.sh | sh
```

Windows, in PowerShell:

```powershell
irm https://dropmorph.vercel.app/install.ps1 | iex
```

All files and their `SHA256SUMS` are on the [Releases page](https://github.com/cod4nitish/DropMorph/releases/latest).
DropMorph is a desktop app: it does not run on Android phones, iPhones or iPads.

## Install

What to do with the file you downloaded:

- **Windows**: run `DropMorph-<version>-win-Setup.exe`. Setup can add the Explorer menu, start at sign-in, the
  `dropmorph` command and optional engines. For the portable version, right-click the ZIP → Properties → **Unblock**,
  then unzip it and run `DropMorph.exe`.
  Releases are not code-signed, so SmartScreen may warn when you run Setup ("More info" → "Run anyway").
- **macOS**: double-click the `.tar.gz`, then run `./install.sh` from the unpacked folder in Terminal. It copies
  DropMorph to `~/Applications`, adds the `dropmorph` command and removes the download quarantine mark, so macOS doesn't
  block the first start. Releases after 1.12.0 also come as a `.dmg` to drag into Applications. Then allow DropMorph
  under System Settings → Privacy & Security → **Accessibility** for the <kbd>Option</kbd>-drag donut.
  The **[macOS guide](docs/MACOS.md)** covers permissions, engines (Homebrew), updates and troubleshooting.
- **Linux**: unpack the package and run `./install.sh` (no root). It checks the system libraries and prints the install
  command for your distribution. Drag gestures need X11; on Wayland, use the drop zone and the file manager's
  right-click menu. The **[Linux guide](docs/LINUX.md)** covers libraries per distribution, X11/Wayland, engines,
  updates and troubleshooting.

**Updates**: DropMorph shows new releases in its menu. On Windows it updates itself; on macOS and Linux, run the new
package's `install.sh` or the one-command install again. Settings and engines are kept either way.

## Screenshots

<table>
  <tr>
    <td width="50%" valign="top">
      <img src="docs/media/donut.svg" alt="The donut: hold Ctrl, drag a file and drop it on a format" width="100%"><br>
      <sub><b>The donut.</b> Hold <kbd>Ctrl</kbd> (<kbd>Option</kbd> on macOS), drag files and drop them on a format.</sub>
    </td>
    <td width="50%" valign="top">
      <img src="docs/media/office-uses.png" alt="Everyday conversions: PDF to Word, smaller PDFs, Excel to PDF, HEIC to JPG, merge and split PDFs, video to MP3" width="100%"><br>
      <sub><b>Everyday office work.</b> PDF ↔ Word, smaller PDFs, Excel → PDF, iPhone HEIC → JPG, merge and split, video → MP3.</sub>
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <img src="docs/media/website-home.png" alt="The DropMorph website" width="100%"><br>
      <sub><b>Website.</b> <a href="https://dropmorph.vercel.app/">dropmorph.vercel.app</a></sub>
    </td>
    <td width="50%" valign="top">
      <img src="docs/media/website-mac-guide.png" alt="The macOS install guide on the website" width="100%"><br>
      <sub><b>Install guides.</b> Step by step for <a href="https://dropmorph.vercel.app/install/">Windows</a>,
      <a href="https://dropmorph.vercel.app/install/mac/">macOS</a> and <a href="https://dropmorph.vercel.app/install/linux/">Linux</a>.</sub>
    </td>
  </tr>
</table>

## Features

- **A donut at the cursor**: hold a key, drag files and drop them on a format. Only formats that fit the files appear.
- **One toolbar per file type**, arranged by drag and drop, with presets for quality, size, DPI, PDF pages and compression,
  video and audio, output folder and file names.
- **Many files at once**, in parallel. Routes with several steps (HWP → HWPX → DOCX) are found automatically.
- **Watch folders** convert every file that lands in them.
- **Two-key shortcuts**: select files and press Alt (Option on macOS) + I, D, V or O for the images, documents, video
  or any-files donut. Every key and donut can be changed on the Shortcuts page.
- **Other ways in**: the right-click menu, a keyboard shortcut, the drop zone and the [`dropmorph` command](#command-line).
- Light and dark themes with your own accent colour, in English, हिन्दी, Español, 한국어, 简体中文 and Polski.

## Formats

| From | To |
|---|---|
| **Images**: JPG, PNG, WEBP, AVIF, TIFF, BMP, GIF, APNG, ICO, ICNS, JPEG XL, JPEG 2000, PSD/PSB, TGA, PPM, HEIC, QOI, DDS, PCX, MNG, CUR, …; GIMP XCF, camera RAW (CR2, CR3, NEF, ARW, DNG, …), DICOM, OpenEXR, HDR, JPEG XR (Windows) read | any of these (HEIC⁵), PDF, SVG (traced), resized, grayscale |
| **Vector**: SVG, SVGZ, EMF, WMF, AI · EPS, PS¹ · CDR, VSD, CGM, ODG, FODG² | PDF (vector), PNG and other images, SVG ↔ SVGZ, SVG → DXF/DWG, PDF → EPS/PS¹, SVG → EMF, WMF, ODG² |
| **PDF** | DOCX, HWPX, TXT, PNG, JPG, TIFF, CBZ, PPTX (a picture of each page), SVG²; merge, split, page ranges, compress (smaller PDFs) |
| **Documents**: DOCX, DOCM, DOTX, EML, Outlook MSG; OFD (Chinese e-invoices, official documents) read · DOC, ODT, FODT, OTT, RTF, WPS, WPD, Pages, ClarisWorks, MacWrite, T602, …² | PDF, DOCX, HWPX, EPUB, TXT, HTML; MSG → EML · ODT, FODT, OTT² |
| **Spreadsheets**: XLSX, XLSM, XLS, XLT, ODS, CSV, TSV · OTS, FODS, Numbers, ET² | PDF, XLSX, ODS, CSV, TSV, JSON, HWPX, HTML · XLT, OTS² |
| **Presentations**: PPTX, PPTM, POTX, POTM, PPSX · PPT, ODP, OTP, FODP, Keynote² | PDF, PNG, JPG, DOCX, HWPX · OTP² |
| **HWP**: HWP, HWPX | PDF, PNG, DOCX, HWPX ↔ HWP, TXT, Markdown, HTML |
| **Text**: TXT, Markdown, HTML; MHTML (saved web pages) read · reStructuredText, LaTeX, Org, AsciiDoc, Typst, MediaWiki, DocBook, Jupyter, Textile, man, OPML³ | PDF, DOCX, HWPX, EPUB · each other, PPTX slides³ |
| **E-books**: EPUB, KEPUB, MOBI, AZW3, AZW, AZW4, FB2, CBZ, CBR, CB7, HTMLZ, TXTZ · LIT, LRF, CHM, PDB, …⁴ | PDF, EPUB, KEPUB (Kobo), DOCX, TXT, HTML, HWPX · MOBI, AZW3⁴ |
| **Video**⁵: MP4, MOV, MKV, WEBM, AVI, WMV, FLV, MPEG, TS, M2TS, 3GP, OGV, VOB, … | MP4 (H.264, HEVC, AV1), WEBM, MOV, MKV, AVI, GIF, APNG, MP3, M4A, 720p, subtitles (SRT, ASS, WebVTT) |
| **Audio**⁵: MP3, M4A, AAC, WAV, FLAC, OGG, OPUS, WMA, AIFF, AMR, APE, WavPack, TTA, DSD (DSF, DFF), DTS, AC3, E-AC3, … | MP3, M4A (AAC, ALAC), WAV, FLAC, OGG, OPUS, AAC, WavPack, TTA, W64, AC3, E-AC3 |
| **Subtitles**: SRT, ASS/SSA, WebVTT, SAMI (SMI), LRC, TTML, MicroDVD, SCC | each other as UTF-8 (Korean CP949 files read correctly) |
| **Archives**: ZIP, 7Z, RAR, TAR, TAR.GZ, TAR.BZ2, TAR.XZ, GZ, ISO, CAB, DMG, ALZ, EGG, … | extract, ZIP, 7Z, TAR, TAR.GZ; any files → one ZIP |
| **CAD**: DWG, DXF | PDF, SVG, PNG, DWG ↔ DXF |
| **Fonts**: TTF, OTF, WOFF, WOFF2, EOT | each other |
| **Data**: JSON, JSON Lines, YAML, TOML, XML · vCard | each other, CSV and XLSX (tables); vCard ↔ CSV |
| **3D models**: GLB, glTF, STL, OBJ (+ MTL), 3MF, PLY; FBX read | each other, PNG preview |
| **Geospatial vectors**: Shapefile, GeoJSON, KML, KMZ, GPX · CSV with coordinates | each other and CSV / XLSX / JSON; projected data (UTM, Web Mercator, Lambert, Albers) comes out as WGS 84 longitude / latitude |

Unmarked formats work out of the box, without Office, Hancom Office or LibreOffice.
Marked ones need an optional engine, downloaded only when needed: ¹ Ghostscript, ² LibreOffice, ³ Pandoc, ⁴ calibre,
⁵ FFmpeg ([details](docs/ENGINES.md)).

## Command line

The `dropmorph` command uses the same engines and presets as the app (Setup or `install.sh` adds it).

```
dropmorph convert photo.heic --to jpg
dropmorph convert *.png --to webp --quality 80 -o converted
dropmorph watch D:\Inbox --to pdf --move-originals
```

Every command and option: `dropmorph help`.

## Development

Build with the .NET 10 SDK on Windows, macOS or Linux:

```bash
git clone https://github.com/cod4nitish/DropMorph.git
cd DropMorph
pwsh build/fetch-engines.ps1 -Only rhwp,pandoc   # optional engines
dotnet run --project src/DropMorph.App
dotnet test
```

[Architecture](docs/ARCHITECTURE.md) · [Engines](docs/ENGINES.md) · [macOS](docs/MACOS.md) · [Linux](docs/LINUX.md) · [Adding a converter](docs/ADDING-A-CONVERTER.md) ·
[Adding a language](docs/ADDING-A-LANGUAGE.md) · [Contributing](CONTRIBUTING.md)

## Privacy

DropMorph converts files on your own computer and never uploads them. It collects no usage data and sends no telemetry.
It connects to the internet only:

- to ask GitHub (`api.github.com`) whether a newer release exists, shortly after start and then every few hours.
  Turn this off in Settings → About ("Check for updates automatically").
- to download the conversion engines you choose to install, from the official locations pinned in
  [`engines.json`](src/DropMorph.Engines/Infrastructure/engines.json).
- when you click a link, which opens your web browser.

<!-- sponsors: src/DropMorph.App/Assets/Sponsors/sponsors.json -->

## License

MIT, see [LICENSE](LICENSE). Bundled third-party components keep their own licenses, listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Made by [Nitish Singh](https://github.com/cod4nitish).
