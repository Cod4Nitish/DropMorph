#!/bin/sh
set -eu

data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_dir/dropmorph/application"
desktop_file="$data_dir/applications/com.cod4nitish.dropmorph.desktop"
python3 - "$data_dir" "$install_dir" <<'PY'
import pathlib
import sys

root, target = map(pathlib.Path, sys.argv[1:])
expected = root.resolve() / 'dropmorph' / 'application'
if target.is_symlink() or target.resolve() != expected:
    raise SystemExit('Refusing to remove an installation outside its expected directory.')
if (target / '.dropmorph-package').read_text(encoding='utf-8').strip() != 'com.cod4nitish.dropmorph':
    raise SystemExit('The directory is not an installation created by DropMorph.')
PY
"$install_dir/DropMorph" --uninstall-cleanup
if [ -L "$HOME/.local/bin/dropmorph" ] && [ "$(readlink "$HOME/.local/bin/dropmorph")" = "$install_dir/dropmorph-cli" ]; then
    rm -- "$HOME/.local/bin/dropmorph"
fi
if [ -f "$desktop_file" ] && grep -q '^X-DropMorph-Managed=true$' "$desktop_file"; then
    rm -- "$desktop_file"
fi
rm -rf -- "$install_dir"
printf '%s\n' 'DropMorph was removed. Your settings and conversion results were kept.'
