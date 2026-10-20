#!/bin/sh
# Installs DropMorph.app into ~/Applications and adds the dropmorph command (~/.local/bin/dropmorph).
# Run it again with a newer package to update: the old app is replaced, settings and engines are kept.
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
install_dir="$HOME/Applications/DropMorph.app"
bin_dir="$HOME/.local/bin"
cli_target="$install_dir/Contents/MacOS/dropmorph-cli"
bundle_id='com.cod4nitish.dropmorph'

fail() {
    printf 'Error: %s\n' "$1" >&2
    exit 1
}

if [ ! -x "$source_dir/DropMorph.app/Contents/MacOS/DropMorph" ]; then
    fail "DropMorph.app was not found next to install.sh. Unpack the whole archive, then run ./install.sh from its folder."
fi
case "$(uname -m)" in
    arm64) machine='osx-arm64' ;;
    x86_64)
        # Rosetta reports x86_64 on Apple silicon too.
        if [ "$(/usr/sbin/sysctl -n sysctl.proc_translated 2>/dev/null || echo 0)" = 1 ]; then machine='osx-arm64'; else machine='osx-x64'; fi ;;
    *) machine='' ;;
esac
# file, not lipo: lipo is part of the developer tools and would ask to install them.
case "$(/usr/bin/file -b "$source_dir/DropMorph.app/Contents/MacOS/DropMorph")" in
    *arm64*) package_arch='arm64' ;;
    *x86_64*) package_arch='x86_64' ;;
    *) package_arch='' ;;
esac
if [ "$machine" = 'osx-x64' ] && [ "$package_arch" = 'arm64' ]; then
    fail 'This package is for Apple silicon Macs (M1 or newer). Download the osx-x64 package for an Intel Mac.'
fi
if [ "$machine" = 'osx-arm64' ] && [ "$package_arch" = 'x86_64' ]; then
    printf '%s\n' 'Note: this is the Intel (osx-x64) package. It runs through Rosetta; the osx-arm64 package is faster on this Mac.'
fi

updating=0
if [ -L "$install_dir" ]; then
    fail "$install_dir is a symbolic link. Remove it before installing DropMorph."
fi
if [ -e "$install_dir" ]; then
    existing_id=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$install_dir/Contents/Info.plist" 2>/dev/null || echo '')
    if [ "$existing_id" != "$bundle_id" ]; then
        fail "$install_dir exists but is not DropMorph. Move it away before installing."
    fi
    updating=1
fi
if [ -e "$bin_dir/dropmorph" ] || [ -L "$bin_dir/dropmorph" ]; then
    if [ ! -L "$bin_dir/dropmorph" ] || [ "$(readlink "$bin_dir/dropmorph")" != "$cli_target" ]; then
        fail "$bin_dir/dropmorph already exists and is not DropMorph's. Remove or rename it before installing."
    fi
fi

mkdir -p "$HOME/Applications" "$bin_dir"
staging="$HOME/Applications/.DropMorph.app.installing.$$"
trap 'rm -rf -- "$staging"' EXIT INT TERM
/usr/bin/ditto "$source_dir/DropMorph.app" "$staging"
# A browser marks downloads as quarantined, and unpacking passes that mark to every file in the app. Running this
# script is the decision to trust the package, so the mark is removed: macOS then opens the app (and the engines inside
# it) without the "cannot be verified" block. Only install a package whose source and checksum you trust.
/usr/bin/xattr -dr com.apple.quarantine "$staging" 2>/dev/null || true

if [ "$updating" = 1 ]; then
    printf '%s\n' 'Updating the existing DropMorph. Your settings and engines are kept.'
    # Asks the running DropMorph to exit and waits for it.
    "$install_dir/Contents/MacOS/DropMorph" --quit >/dev/null 2>&1 || true
    rm -rf -- "$install_dir"
fi
mv -- "$staging" "$install_dir"
trap - EXIT INT TERM
if [ ! -L "$bin_dir/dropmorph" ]; then
    ln -s "$cli_target" "$bin_dir/dropmorph"
fi

if [ "$updating" = 1 ]; then
    printf '%s\n' "Updated $install_dir"
else
    printf '%s\n' "Installed $install_dir"
fi
printf '%s\n' "Command line: $bin_dir/dropmorph"
case ":$PATH:" in
    *":$bin_dir:"*) ;;
    *)
        printf '%s\n' "$bin_dir is not on your PATH yet. To use the dropmorph command, run once:" \
            "  echo 'export PATH=\"\$HOME/.local/bin:\$PATH\"' >> ~/.zprofile && source ~/.zprofile" ;;
esac
printf '%s\n' '' 'Next:' \
    '  1. DropMorph opens now. Allow it under System Settings > Privacy & Security > Accessibility' \
    '     (needed for the Option-drag donut).' \
    '  2. The first time you use a selection shortcut, allow DropMorph to control Finder.'
if [ "$updating" = 1 ]; then
    printf '%s\n' '  After an update, if Option-drag stops working although Accessibility shows DropMorph switched on:' \
        '  select DropMorph in that list, remove it with "-", then add it again with "+" (or reopen DropMorph).'
fi
/usr/bin/open "$install_dir" 2>/dev/null || true
