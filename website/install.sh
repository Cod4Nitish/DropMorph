#!/bin/sh
# DropMorph one-line installer for macOS and Linux:
#
#   curl -fsSL https://dropmorph.vercel.app/install.sh | sh
#
# Finds the latest release on GitHub, downloads the package for this computer (osx-arm64, osx-x64, linux-x64 or
# linux-arm64), checks its SHA-256 against the release's checksum file and runs the package's own install.sh, which
# installs for the current user only (no sudo) and updates an existing installation in place.
# Set DROPMORPH_VERSION=1.12.0 to install a specific version.
set -eu

repo='https://github.com/Cod4Nitish/DropMorph'

say() { printf '%s\n' "$*"; }
fail() { printf 'Error: %s\n' "$*" >&2; exit 1; }

command -v curl >/dev/null 2>&1 || fail 'curl is required.'
command -v tar >/dev/null 2>&1 || fail 'tar is required.'

case "$(uname -s)" in
    Darwin)
        os='osx'
        major=$(sw_vers -productVersion | cut -d. -f1)
        [ "$major" -ge 14 ] 2>/dev/null || fail "DropMorph needs macOS 14 or newer (this Mac has $(sw_vers -productVersion))."
        ;;
    Linux) os='linux' ;;
    *) fail 'This installer is for macOS and Linux. On Windows, run in PowerShell:  irm https://dropmorph.vercel.app/install.ps1 | iex' ;;
esac
case "$(uname -m)" in
    x86_64 | amd64)
        arch='x64'
        # Rosetta reports x86_64 on Apple silicon.
        if [ "$os" = osx ] && [ "$(sysctl -n sysctl.proc_translated 2>/dev/null || echo 0)" = 1 ]; then arch='arm64'; fi
        ;;
    arm64 | aarch64) arch='arm64' ;;
    *) fail "No DropMorph package for this processor ($(uname -m))." ;;
esac
runtime="$os-$arch"

if [ -n "${DROPMORPH_VERSION:-}" ]; then
    version=${DROPMORPH_VERSION#v}
else
    # github.com/<repo>/releases/latest redirects to .../releases/tag/v<version> (no API rate limit).
    latest=$(curl -fsSLI -o /dev/null -w '%{url_effective}' "$repo/releases/latest") || fail 'Could not reach GitHub.'
    version=${latest##*/v}
    case "$version" in
        [0-9]*.[0-9]*.[0-9]*) ;;
        *) fail "Could not find the latest DropMorph version ($latest)." ;;
    esac
fi
package="DropMorph-$version-$runtime"
base="$repo/releases/download/v$version"

work=$(mktemp -d 2>/dev/null || mktemp -d -t dropmorph)
trap 'rm -rf -- "$work"' EXIT INT TERM

say "Downloading DropMorph $version for $runtime..."
curl -fL --progress-bar -o "$work/$package.tar.gz" "$base/$package.tar.gz" || fail "Download failed: $base/$package.tar.gz"
curl -fsSL -o "$work/sums.txt" "$base/$package-SHA256SUMS.txt" || fail 'Could not download the checksum file.'

expected=$(grep " $package.tar.gz\$" "$work/sums.txt" | cut -d' ' -f1)
if command -v sha256sum >/dev/null 2>&1; then
    actual=$(sha256sum "$work/$package.tar.gz" | cut -d' ' -f1)
else
    actual=$(shasum -a 256 "$work/$package.tar.gz" | cut -d' ' -f1)
fi
[ -n "$expected" ] && [ "$expected" = "$actual" ] || fail 'Checksum mismatch: the download is incomplete or was changed. Nothing was installed.'
say 'Checksum OK.'

tar -xzf "$work/$package.tar.gz" -C "$work"
# The package's installer reads from the terminal, not from this script's pipe.
sh "$work/$package/install.sh" </dev/null
