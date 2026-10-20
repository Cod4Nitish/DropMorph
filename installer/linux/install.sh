#!/bin/sh
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_dir/dropmorph/application"
bin_dir="$HOME/.local/bin"
desktop_file="$data_dir/applications/com.cod4nitish.dropmorph.desktop"
if [ -e "$install_dir" ] || [ -L "$install_dir" ]; then
    printf '%s\n' "An installation already exists at $install_dir. Run its uninstall.sh first."
    exit 1
fi
if [ -e "$bin_dir/dropmorph" ] || [ -L "$bin_dir/dropmorph" ]; then
    printf '%s\n' "$bin_dir/dropmorph already exists. Remove or rename it before installing DropMorph."
    exit 1
fi
if [ -e "$desktop_file" ] || [ -L "$desktop_file" ]; then
    printf '%s\n' "$desktop_file already exists. Remove or rename it before installing DropMorph."
    exit 1
fi
command -v python3 >/dev/null
test -x "$source_dir/DropMorph"
test -x "$source_dir/dropmorph-cli"
mkdir -p "$data_dir/dropmorph" "$bin_dir" "$data_dir/applications"
cp -R "$source_dir" "$install_dir"
printf '%s\n' 'com.cod4nitish.dropmorph' > "$install_dir/.dropmorph-package"
ln -s "$install_dir/dropmorph-cli" "$bin_dir/dropmorph"
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
    'Terminal=false\nCategories=Utility;\nX-DropMorph-Managed=true\n', encoding='utf-8')
PY
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$data_dir/applications"
fi
printf '%s\n' "Installed DropMorph at $install_dir" "CLI: $bin_dir/dropmorph (add $bin_dir to PATH if needed)."
