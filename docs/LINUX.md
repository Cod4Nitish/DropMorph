# DropMorph on Linux

DropMorph runs on **Ubuntu 22.04 or newer** and other distributions with **glibc 2.35 or newer** (Debian 12, Fedora 36+,
Linux Mint 21+, Pop!_OS 22.04+, Arch, openSUSE Tumbleweed, …), on x64 and ARM64. The .NET runtime is included. It
installs for your user only; no root is needed except for the system libraries.

- [1. Pick the right download](#1-pick-the-right-download)
- [2. System libraries](#2-system-libraries)
- [3. Install](#3-install)
- [4. Use it](#4-use-it)
- [5. X11 and Wayland](#5-x11-and-wayland)
- [6. Engines for video, audio and old formats](#6-engines-for-video-audio-and-old-formats)
- [7. Update](#7-update)
- [8. Uninstall](#8-uninstall)
- [Troubleshooting](#troubleshooting)

## 1. Pick the right download

Run `uname -m` in a terminal:

| `uname -m` | Download |
|---|---|
| `x86_64` | `DropMorph-<version>-linux-x64.tar.gz` |
| `aarch64` (Raspberry Pi 4/5 with a 64-bit OS, ARM laptops) | `DropMorph-<version>-linux-arm64.tar.gz` |

Both are on the [Releases page](https://github.com/cod4nitish/DropMorph/releases/latest), each with a
`-SHA256SUMS.txt` (`sha256sum -c DropMorph-*-SHA256SUMS.txt` checks the download).

## 2. System libraries

The desktop app needs ICU, fontconfig, FreeType and a few X11 libraries. `install.sh` checks for them and prints the
command for your distribution when one is missing. To install them up front:

**Ubuntu 24.04+ / Debian 13 / Mint 22**

```sh
sudo apt install libicu74 libssl3t64 libfontconfig1 libfreetype6 libx11-6 libice6 libsm6 libxrandr2 libxkbcommon0 libxi6 libxtst6 libxt6 fonts-noto-cjk python3
```

**Ubuntu 22.04 / Debian 12 / Mint 21**

```sh
sudo apt install libicu70 libssl3 libfontconfig1 libfreetype6 libx11-6 libice6 libsm6 libxrandr2 libxkbcommon0 libxi6 libxtst6 libxt6 fonts-noto-cjk python3
```

(Debian 12 has `libicu72`; find the name on any release with `apt-cache search libicu`.)

**Fedora**

```sh
sudo dnf install libicu fontconfig freetype libX11 libICE libSM libXrandr libxkbcommon libXi libXtst libXt google-noto-sans-cjk-fonts python3
```

**Arch / Manjaro**

```sh
sudo pacman -S --needed icu fontconfig freetype2 libx11 libice libsm libxrandr libxkbcommon libxi libxtst libxt noto-fonts-cjk python
```

**openSUSE**

```sh
sudo zypper install libicu fontconfig libfreetype6 libX11-6 libICE6 libSM6 libXrandr2 libxkbcommon0 libXi6 libXtst6 libXt6 noto-sans-cjk-fonts python3
```

The CJK fonts are only needed to show Chinese, Japanese and Korean text.

## 3. Install

```sh
cd ~/Downloads
tar -xzf DropMorph-*-linux-x64.tar.gz          # or linux-arm64
cd DropMorph-*-linux-x64/
./install.sh
```

`install.sh` (needs Python 3, no root):

- copies DropMorph to `~/.local/share/dropmorph/application`;
- adds **DropMorph** to your application menu (`~/.local/share/applications`);
- adds the `dropmorph` command at `~/.local/bin/dropmorph` and tells you if that folder is not on your `PATH`;
- checks the system libraries and the package's architecture.

To try DropMorph without installing, run `./DropMorph` in the unpacked folder (`./dropmorph-cli help` for the command
line).

## 4. Use it

Open **DropMorph** from the application menu. It sits in the system tray; closing its window keeps it running there.

1. Hold <kbd>Ctrl</kbd>.
2. Start dragging one or more files from your file manager or the desktop.
3. The donut opens at the cursor with the formats that fit those files.
4. Drop on a format. The result is saved next to the original.

Other ways in:

- **Right-click menu**: DropMorph → Settings → General → *Add "Convert with DropMorph" to the file manager*. It adds
  an entry for Nautilus (GNOME Files: *Scripts → DropMorph*), Nemo, Dolphin (KDE) and Thunar (Xfce).
- **Drop zone**: drop files on DropMorph's window.
- **Shortcuts**: select files and press <kbd>Alt</kbd> + <kbd>I</kbd> (images donut), <kbd>D</kbd> (documents),
  <kbd>V</kbd> (video) or <kbd>O</kbd> (any files). Change them on the Shortcuts page.
- **Terminal**: `dropmorph convert photo.heic --to jpg`, `dropmorph watch ~/Inbox --to pdf` (`dropmorph help`).

**Settings → General → Start DropMorph when I sign in** keeps the donut ready after every restart
(`~/.config/autostart/dropmorph.desktop`).

## 5. X11 and Wayland

The Ctrl-drag donut needs to see the mouse in other apps and place a window at the cursor. **X11** allows both.
**Wayland** doesn't, so on Wayland use the drop zone, the right-click menu and the command line. Check with
`echo $XDG_SESSION_TYPE`.

To get the donut on a Wayland desktop:

- **GNOME, Cinnamon, Xfce, …**: on the login screen, click the gear icon and choose **"GNOME on Xorg"** (or the session
  without "Wayland").
- **KDE Plasma 6**: choose **"Plasma (X11)"**, or try the experimental Wayland support described in `INSTALL.txt` in the
  package (it needs the `input` group and a KWin script).

## 6. Engines for video, audio and old formats

7-Zip and rhwp come with the app. Everything else is optional:

| Engine | Needed for | How to get it on Linux |
|---|---|---|
| **FFmpeg** | video and audio | Settings → Engines → Install, or your distribution's `ffmpeg` package |
| **Pandoc** | LaTeX, reStructuredText, Org, … | Settings → Engines → Install |
| **calibre** | MOBI/AZW3 output, LIT, CHM, … | Settings → Engines → Install (may need `libxcb-cursor0`, `libxcb-xinerama0`) |
| **LibreOffice** | DOC, PPT, ODT, Pages, Keynote, … | `sudo apt install libreoffice` (or `dnf`/`pacman`) |
| **Ghostscript** | EPS, PS | `sudo apt install ghostscript` |

Click **Check again** in Settings → Engines after installing a package. DropMorph finds engines on your `PATH` and in
the usual install folders (`/usr/lib/libreoffice`, `/opt/calibre`, …).

## 7. Update

DropMorph tells you when a new version is out. Download the new package, unpack it and run its `./install.sh`: it
replaces the installed app and keeps your settings, presets and engines.

## 8. Uninstall

```sh
~/.local/share/dropmorph/application/uninstall.sh
```

It removes the app, the menu entry, the `dropmorph` command, autostart, the file-manager entries and the engines
DropMorph downloaded. Your settings (`~/.config/DropMorph`) and converted files are kept; delete that folder too for a
clean slate.

## Troubleshooting

### DropMorph doesn't start

Run it from a terminal to see the error:

```sh
~/.local/share/dropmorph/application/DropMorph
```

- `Couldn't find a valid ICU package` → install the libicu package from [step 2](#2-system-libraries).
- `libX11.so.6` / `libXtst.so.6` … *cannot open shared object file* → install the X11 libraries from step 2.
- `GLIBC_2.35 not found` → the distribution is too old; Ubuntu 22.04 or newer is needed.

### Ctrl-drag does nothing

- Check that DropMorph is running (tray icon).
- `echo $XDG_SESSION_TYPE` says `wayland`: see [X11 and Wayland](#5-x11-and-wayland).
- The app you drag from may be on the excluded list (Settings → Shortcuts).

### `dropmorph: command not found`

```sh
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.profile && . ~/.profile
```

### The menu entry is missing

Log out and in again, or run `update-desktop-database ~/.local/share/applications`.

### Logs

`~/.config/DropMorph/logs`. Attach the newest log to a
[bug report](https://github.com/cod4nitish/DropMorph/issues/new/choose).
