# DropMorph on macOS

DropMorph runs on **macOS 14 Sonoma or newer**, on Apple silicon (M1 and later) and Intel Macs. The .NET runtime is
included; you don't need to install anything else to convert images, PDF, Office documents, HWP, e-books, archives,
fonts, CAD, 3D models and map data.

- [1. Pick the right download](#1-pick-the-right-download)
- [2. Install](#2-install)
- [3. First start and permissions](#3-first-start-and-permissions)
- [4. Use it](#4-use-it)
- [5. Engines for video, audio and old formats](#5-engines-for-video-audio-and-old-formats)
- [6. Update](#6-update)
- [7. Uninstall](#7-uninstall)
- [Troubleshooting](#troubleshooting)
- [Building from source on a Mac](#building-from-source-on-a-mac)

## 1. Pick the right download

Open  **Apple menu → About This Mac**:

| About This Mac shows | Download |
|---|---|
| **Chip**: Apple M1, M2, M3, M4, … | `DropMorph-<version>-osx-arm64.tar.gz` (or `.dmg`) |
| **Processor**: … Intel … | `DropMorph-<version>-osx-x64.tar.gz` (or `.dmg`) |

Both are on the [Releases page](https://github.com/cod4nitish/DropMorph/releases/latest). Each has a
`-SHA256SUMS.txt`; to check a download, run `shasum -a 256 DropMorph-*.tar.gz` in Terminal and compare.

## 2. Install

### Option A: with Terminal (recommended)

This is the most reliable way: it also adds the `dropmorph` command and lets macOS open the app without a block.

1. Double-click the downloaded `.tar.gz`. Finder unpacks it into a folder `DropMorph-<version>-osx-…`.
2. Open **Terminal** (Spotlight: <kbd>⌘</kbd> <kbd>Space</kbd>, type *Terminal*).
3. Type `sh ` (with a space), drag **install.sh** from the folder into the Terminal window and press <kbd>Return</kbd>.

   Or, with the default Downloads folder:

   ```sh
   cd ~/Downloads/DropMorph-*-osx-*/ && ./install.sh
   ```

`install.sh`:

- copies **DropMorph.app** to `~/Applications` (your own Applications folder, no admin password needed);
- adds the `dropmorph` command at `~/.local/bin/dropmorph` and tells you if that folder is not on your `PATH` yet;
- removes the download quarantine mark, so macOS doesn't stop the first start with "cannot be verified";
- warns when the package doesn't fit your Mac (Intel package on Apple silicon and the other way round);
- opens DropMorph.

### Option B: drag and drop (.dmg)

1. Open the `.dmg`.
2. Drag **DropMorph.app** onto the **Applications** folder in the same window.
3. Open DropMorph from Applications or Launchpad. The first start is blocked once; see
   [Gatekeeper](#macos-says-dropmorph-not-opened--cannot-be-verified--is-damaged) below.

The disk image also contains `install.sh`, so you can use Option A straight from it.

> DropMorph releases are ad-hoc signed and not notarized by Apple (that needs a paid Apple Developer ID). Every release
> is built by GitHub Actions from the public source. Only install a package you downloaded from the
> [official Releases page](https://github.com/cod4nitish/DropMorph/releases).

## 3. First start and permissions

DropMorph lives in the **menu bar** (the drop icon at the top right). Closing its window keeps it running there.

| Permission | Where | What for |
|---|---|---|
| **Accessibility** | System Settings → Privacy & Security → Accessibility → switch on **DropMorph** | The <kbd>⌥ Option</kbd>-drag donut (DropMorph has to see the mouse and keys in other apps). It works as soon as you switch it on. |
| **Automation → Finder** | asked the first time; System Settings → Privacy & Security → Automation | Selection shortcuts, which read the files selected in Finder. |
| Desktop / Documents / Downloads folders | asked the first time you convert a file there | Saving the result next to the original. |

DropMorph's **Settings → Shortcuts** page has a button that opens the Accessibility settings directly.

Also in DropMorph's settings:

- **General → Start DropMorph when I sign in** keeps the donut ready after every restart (a LaunchAgent in `~/Library/LaunchAgents`).
- **General → "Convert with DropMorph"** in Finder's right-click menu (a macOS service; it can also be switched under
  System Settings → Keyboard → Keyboard Shortcuts → Services → Files and Folders).
- **General → Show DropMorph in the Dock** if you prefer a Dock icon over the menu bar only.

## 4. Use it

1. Hold <kbd>⌥ Option</kbd>.
2. Start dragging one or more files in Finder or on the desktop.
3. The donut opens at the cursor with the formats that fit those files.
4. Drop on a format. The result is saved next to the original.

Other ways in:

- **Right-click** files in Finder → **Quick Actions / Services → Convert with DropMorph**.
- **Drop zone**: open DropMorph's window and drop files on it.
- **Shortcuts**: select files in Finder and press <kbd>⌥ Option</kbd> + <kbd>I</kbd> (images donut), <kbd>D</kbd>
  (documents), <kbd>V</kbd> (video) or <kbd>O</kbd> (any files), or <kbd>⌃ Control</kbd> + <kbd>⌥ Option</kbd> +
  <kbd>Space</kbd> for the donut that fits the files. Change them, or add your own, on the Shortcuts page.
- **Terminal**: `dropmorph convert photo.heic --to jpg`, `dropmorph watch ~/Inbox --to pdf` (`dropmorph help` lists
  everything).

The modifier key can be changed on the **Shortcuts** page (for example to <kbd>⌃ Control</kbd>+<kbd>⌥ Option</kbd>), and
apps where Option-drag should keep its normal meaning can be excluded there.

## 5. Engines for video, audio and old formats

7-Zip and rhwp come with the app. Everything else is optional:

| Engine | Needed for | How to get it on macOS |
|---|---|---|
| **FFmpeg** | video and audio (MP4, MOV, MP3, WAV, …) | `brew install ffmpeg` |
| **Ghostscript** | EPS, PS | `brew install ghostscript` |
| **LibreOffice** | DOC, PPT, ODT, Pages, Numbers, Keynote, … | DropMorph → Settings → Engines → Install, or an existing LibreOffice.app |
| **Pandoc** | LaTeX, reStructuredText, Org, … | Settings → Engines → Install |
| **calibre** | MOBI/AZW3 output, LIT, CHM, … | Settings → Engines → Install, or an existing calibre.app |

FFmpeg and Ghostscript come from [Homebrew](https://brew.sh). Without Homebrew:

```sh
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
brew install ffmpeg ghostscript
```

Then click **Check again** in Settings → Engines. DropMorph looks in `/opt/homebrew/bin` (Apple silicon),
`/usr/local/bin` (Intel) and MacPorts' `/opt/local/bin`, or you can select a folder yourself. On Apple silicon, video
encoding uses the Mac's hardware encoder (VideoToolbox).

## 6. Update

DropMorph tells you when a new version is out (Settings → About can switch this off). To install it, download the new
package and run its `install.sh` again (or drag the new app over the old one). Settings, presets and downloaded
engines are kept.

Because each build has a new signature, macOS may treat the updated app as a different one for Accessibility: if
Option-drag stops working, see [below](#option-drag-does-nothing).

## 7. Uninstall

From the unpacked package folder:

```sh
./uninstall.sh
```

It quits DropMorph, removes `~/Applications/DropMorph.app`, the `dropmorph` command, the login item, the Finder
service and the engines DropMorph downloaded. Your settings (`~/Library/Application Support/DropMorph`) and converted
files are kept; delete that folder too for a clean slate.

A copy you dragged into `/Applications`: first run
`/Applications/DropMorph.app/Contents/MacOS/DropMorph --uninstall-cleanup`, then move the app to the Bin.

## Troubleshooting

### macOS says "DropMorph Not Opened" / "cannot be verified" / "is damaged"

This is Gatekeeper blocking an app that isn't notarized. Either:

- **System Settings → Privacy & Security**, scroll down to *"DropMorph" was blocked…* → **Open Anyway** → enter your
  password → **Open Anyway** again (macOS 15 Sequoia and later no longer offer this through right-click → Open); or
- remove the quarantine mark in Terminal, then open the app normally:

  ```sh
  xattr -dr com.apple.quarantine ~/Applications/DropMorph.app      # or /Applications/DropMorph.app
  ```

`install.sh` does the second step for you.

### Option-drag does nothing

- Check that DropMorph is running (drop icon in the menu bar).
- System Settings → Privacy & Security → **Accessibility**: DropMorph must be switched on. If it already is (typically
  after an update), select it, remove it with **−**, add `~/Applications/DropMorph.app` again with **+**, and switch it
  on.
- The app you drag from may be on the excluded list (Settings → Shortcuts).

### "Convert with DropMorph" is missing from the right-click menu

Switch it on in DropMorph → Settings → General, and check System Settings → Keyboard → Keyboard Shortcuts → Services →
*Files and Folders*. In Finder it is under **Quick Actions** or **Services** at the bottom of the menu. Logging out and
in again refreshes the menu if macOS still shows the old state.

### Selection shortcut says it can't read the Finder selection

System Settings → Privacy & Security → **Automation** → DropMorph → switch on **Finder**.

### Video or audio files don't show any formats

Install FFmpeg (`brew install ffmpeg`) and click **Check again** in Settings → Engines.

### `dropmorph: command not found`

`~/.local/bin` is not on your `PATH`:

```sh
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zprofile && source ~/.zprofile
```

### Logs

`~/Library/Application Support/DropMorph/logs`. Attach the newest log to a
[bug report](https://github.com/cod4nitish/DropMorph/issues/new/choose).

## Building from source on a Mac

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and PowerShell (`brew install --cask dotnet-sdk powershell`).

```sh
git clone https://github.com/cod4nitish/DropMorph.git && cd DropMorph
pwsh build/fetch-portable-engines.ps1 -Runtime osx-arm64 -Destination engines   # rhwp and 7-Zip (osx-x64 on Intel)
dotnet run --project src/DropMorph.App
dotnet test
```

To build the release package (`Releases/DropMorph-<version>-osx-<arch>.tar.gz`, `.dmg` and checksums) on the Mac you
run it on:

```sh
pwsh build/build-portable.ps1                    # ad-hoc signed, for testing
pwsh build/build-portable.ps1 -SigningIdentity "Developer ID Application: Your Name (TEAMID)"
```

With a Developer ID identity the binaries are signed with the hardened runtime and a timestamp. For public
distribution, notarize the disk image afterwards:

```sh
xcrun notarytool submit Releases/DropMorph-*-osx-arm64.dmg --keychain-profile <profile> --wait
xcrun stapler staple Releases/DropMorph-*-osx-arm64.dmg
```
