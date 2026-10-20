#!/bin/sh
set -eu

install_dir="$HOME/Applications/DropMorph.app"
if [ -L "$install_dir" ] || [ ! -d "$install_dir" ]; then
    printf '%s\n' "No ordinary DropMorph app bundle was found at $install_dir."
    exit 1
fi
resolved_parent=$(CDPATH= cd -- "$HOME/Applications" && pwd -P)
resolved_app=$(CDPATH= cd -- "$install_dir" && pwd -P)
if [ "$resolved_app" != "$resolved_parent/DropMorph.app" ]; then
    printf '%s\n' 'Refusing to remove an app outside the expected Applications directory.'
    exit 1
fi
bundle_id=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$install_dir/Contents/Info.plist")
if [ "$bundle_id" != 'com.cod4nitish.dropmorph' ]; then
    printf '%s\n' 'The bundle is not DropMorph.'
    exit 1
fi
"$install_dir/Contents/MacOS/DropMorph" --uninstall-cleanup
if [ -L "$HOME/.local/bin/dropmorph" ] && [ "$(readlink "$HOME/.local/bin/dropmorph")" = "$install_dir/Contents/MacOS/dropmorph-cli" ]; then
    rm -- "$HOME/.local/bin/dropmorph"
fi
rm -rf -- "$install_dir"
printf '%s\n' 'DropMorph was removed. Your settings and conversion results were kept.'
