#!/bin/sh
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
install_dir="$HOME/Applications/DropMorph.app"
bin_dir="$HOME/.local/bin"
if [ -e "$install_dir" ] || [ -L "$install_dir" ]; then
    printf '%s\n' "An installation already exists at $install_dir. Run uninstall.sh first."
    exit 1
fi
if [ -e "$bin_dir/dropmorph" ] || [ -L "$bin_dir/dropmorph" ]; then
    printf '%s\n' "$bin_dir/dropmorph already exists. Remove or rename it before installing DropMorph."
    exit 1
fi
test -x "$source_dir/DropMorph.app/Contents/MacOS/DropMorph"
mkdir -p "$HOME/Applications" "$bin_dir"
/usr/bin/ditto "$source_dir/DropMorph.app" "$install_dir"
ln -s "$install_dir/Contents/MacOS/dropmorph-cli" "$bin_dir/dropmorph"
printf '%s\n' "Installed $install_dir" "CLI: $bin_dir/dropmorph (add $bin_dir to PATH if needed)."
printf '%s\n' 'Open DropMorph from Applications. Grant Accessibility and Finder Automation access when requested.'
