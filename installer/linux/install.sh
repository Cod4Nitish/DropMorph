#!/bin/sh
# Installs DropMorph for the current user (no root): the app in ~/.local/share/dropmorph/application, an application
# launcher and the dropmorph command (~/.local/bin/dropmorph).
# Run it again with a newer package to update: the old app is replaced, settings and engines are kept.
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_dir/dropmorph/application"
bin_dir="$HOME/.local/bin"
desktop_file="$data_dir/applications/com.cod4nitish.dropmorph.desktop"
package_id='com.cod4nitish.dropmorph'

fail() {
    printf 'Error: %s\n' "$1" >&2
    exit 1
}

if [ ! -x "$source_dir/DropMorph" ] || [ ! -x "$source_dir/dropmorph-cli" ]; then
    fail 'DropMorph was not found next to install.sh. Unpack the whole archive (tar -xzf), then run ./install.sh from its folder.'
fi
if ! command -v python3 >/dev/null 2>&1; then
    fail 'install.sh needs Python 3. Install it first (Ubuntu/Debian: sudo apt install python3), then run ./install.sh again.'
fi
case "$(uname -m)" in
    x86_64 | amd64) machine='x64' ;;
    aarch64 | arm64) machine='arm64' ;;
    *) machine='' ;;
esac
case "$(basename -- "$source_dir")" in
    *-linux-x64) package_arch='x64' ;;
    *-linux-arm64) package_arch='arm64' ;;
    *) package_arch='' ;;
esac
if [ -n "$machine" ] && [ -n "$package_arch" ] && [ "$machine" != "$package_arch" ]; then
    fail "This is the linux-$package_arch package, but this computer needs linux-$machine. Download that one instead."
fi

updating=0
if [ -L "$install_dir" ]; then
    fail "$install_dir is a symbolic link. Remove it before installing DropMorph."
fi
if [ -e "$install_dir" ]; then
    if [ "$(cat "$install_dir/.dropmorph-package" 2>/dev/null || true)" != "$package_id" ]; then
        fail "$install_dir exists but was not installed by DropMorph. Move it away before installing."
    fi
    updating=1
fi
if [ -e "$bin_dir/dropmorph" ] || [ -L "$bin_dir/dropmorph" ]; then
    if [ ! -L "$bin_dir/dropmorph" ] || [ "$(readlink "$bin_dir/dropmorph")" != "$install_dir/dropmorph-cli" ]; then
        fail "$bin_dir/dropmorph already exists and is not DropMorph's. Remove or rename it before installing."
    fi
fi
if [ -e "$desktop_file" ] || [ -L "$desktop_file" ]; then
    if [ -L "$desktop_file" ] || ! grep -q '^X-DropMorph-Managed=true$' "$desktop_file"; then
        fail "$desktop_file already exists and is not DropMorph's. Remove or rename it before installing."
    fi
fi

# The libraries the desktop app needs (INSTALL.txt). Missing ones are reported with the command that installs them;
# the CLI works without the X11 ones.
# ldconfig is in /sbin on Debian, outside a normal user's PATH.
ldconfig=$(command -v ldconfig 2>/dev/null || true)
for candidate in /sbin/ldconfig /usr/sbin/ldconfig; do
    if [ -z "$ldconfig" ] && [ -x "$candidate" ]; then ldconfig=$candidate; fi
done
if [ -n "$ldconfig" ]; then
    libraries=$("$ldconfig" -p 2>/dev/null || true)
    missing=''
    for library in libicuuc.so libfontconfig.so.1 libfreetype.so.6 libX11.so.6 libICE.so.6 libSM.so.6 libXrandr.so.2 \
        libxkbcommon.so.0 libXi.so.6 libXtst.so.6 libXt.so.6; do
        case "$libraries" in
            *"$library"*) ;;
            *) missing="$missing $library" ;;
        esac
    done
    if [ -n "$missing" ]; then
        distro=''
        version=''
        if [ -r /etc/os-release ]; then
            distro=$(. /etc/os-release && printf '%s %s' "${ID:-}" "${ID_LIKE:-}")
            version=$(. /etc/os-release && printf '%s' "${VERSION_ID:-}")
        fi
        printf '%s\n' "Warning: some libraries DropMorph needs are missing:$missing"
        case "$distro" in
            *ubuntu* | *debian*)
                if [ "$version" = '22.04' ]; then icu='libicu70'; ssl='libssl3'; else icu='libicu74'; ssl='libssl3t64'; fi
                printf '%s\n' 'Install them with:' \
                    "  sudo apt install $icu $ssl libfontconfig1 libfreetype6 libx11-6 libice6 libsm6 libxrandr2 libxkbcommon0 libxi6 libxtst6 libxt6 fonts-noto-cjk"
                if [ "$version" != '22.04' ] && [ "$version" != '24.04' ]; then
                    printf '%s\n' '  (On other releases, find the libicu package name with: apt-cache search libicu)'
                fi ;;
            *fedora* | *rhel* | *centos*)
                printf '%s\n' 'Install them with:' \
                    '  sudo dnf install libicu fontconfig freetype libX11 libICE libSM libXrandr libxkbcommon libXi libXtst libXt google-noto-sans-cjk-fonts' ;;
            *arch*)
                printf '%s\n' 'Install them with:' \
                    '  sudo pacman -S --needed icu fontconfig freetype2 libx11 libice libsm libxrandr libxkbcommon libxi libxtst libxt noto-fonts-cjk' ;;
            *opensuse* | *suse*)
                printf '%s\n' 'Install them with:' \
                    '  sudo zypper install libicu fontconfig libfreetype6 libX11-6 libICE6 libSM6 libXrandr2 libxkbcommon0 libXi6 libXtst6 libXt6 noto-sans-cjk-fonts' ;;
            *)
                printf '%s\n' 'Install the matching packages of your distribution (see INSTALL.txt).' ;;
        esac
        printf '%s\n' 'DropMorph is installed anyway; the desktop app starts once they are there.' ''
    fi
fi

mkdir -p "$data_dir/dropmorph" "$bin_dir" "$data_dir/applications"
staging="$data_dir/dropmorph/.application.installing.$$"
trap 'rm -rf -- "$staging"' EXIT INT TERM
cp -R "$source_dir" "$staging"
printf '%s\n' "$package_id" > "$staging/.dropmorph-package"
if [ "$updating" = 1 ]; then
    printf '%s\n' 'Updating the existing DropMorph. Your settings and engines are kept.'
    # Asks the running DropMorph to exit and waits for it.
    "$install_dir/DropMorph" --quit >/dev/null 2>&1 || true
    rm -rf -- "$install_dir"
fi
mv -- "$staging" "$install_dir"
trap - EXIT INT TERM
if [ ! -L "$bin_dir/dropmorph" ]; then
    ln -s "$install_dir/dropmorph-cli" "$bin_dir/dropmorph"
fi
python3 - "$install_dir" "$desktop_file" <<'PY'
import pathlib
import sys

app, desktop = map(pathlib.Path, sys.argv[1:])
def quoted_exec(value):
    value = value.replace('%', '%%')
    for character in ('\\', '"', '`', '$'):
        value = value.replace(character, '\\' + character)
    return '"' + value.replace('\\', '\\\\').replace('\n', '\\n').replace('\r', '\\r') + '"'

def desktop_value(value):
    return str(value).replace('\\', '\\\\').replace('\n', '\\n').replace('\r', '\\r')

desktop.write_text(
    '[Desktop Entry]\nType=Application\nName=DropMorph\n'
    'Comment=Convert files with DropMorph\n'
    f'Exec={quoted_exec(str(app / "DropMorph"))} --convert %F\n'
    f'Icon={desktop_value(app / "dropmorph.png")}\n'
    'Terminal=false\nCategories=Utility;\nKeywords=convert;converter;pdf;image;video;\nX-DropMorph-Managed=true\n',
    encoding='utf-8')
PY
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$data_dir/applications" || true
fi

if [ "$updating" = 1 ]; then
    printf '%s\n' "Updated DropMorph at $install_dir"
else
    printf '%s\n' "Installed DropMorph at $install_dir"
fi
printf '%s\n' "Command line: $bin_dir/dropmorph"
case ":$PATH:" in
    *":$bin_dir:"*) ;;
    *)
        printf '%s\n' "$bin_dir is not on your PATH yet. To use the dropmorph command, run once:" \
            "  echo 'export PATH=\"\$HOME/.local/bin:\$PATH\"' >> ~/.profile && . ~/.profile" ;;
esac
printf '%s\n' 'Open DropMorph from your application menu.'
if [ "${XDG_SESSION_TYPE:-}" = 'wayland' ]; then
    printf '%s\n' 'This is a Wayland session: the Ctrl-drag donut needs X11. Use the drop window and the file manager' \
        "menu, or choose an \"X11\"/\"Xorg\" session on the login screen (see INSTALL.txt for KDE Plasma's experimental option)."
fi
