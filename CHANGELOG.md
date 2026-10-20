# Changelog

What changed in each version of DropMorph, for the people using it. The release workflow puts the section of the
version it publishes under "What's Changed" in the GitHub release notes, above the list of pull requests.

Add a `## <version>` section before tagging a release. Write what someone using DropMorph notices: what's new, where to
find it, what it does and doesn't do.

Start the section with an `<!-- update -->` block (hidden on GitHub). The app's update notice shows its level and its
one-line summaries in the user's language (`ko`, `en`, `zh`, `pl`; a line per change, a language may repeat; a language
without lines shows the English ones):

- `required`: wrong or lost results, security, DropMorph not starting. The card shows every time DropMorph starts.
- `recommended`: bug fixes. The card shows once per version.
- `optional`: new features only. No card, the sidebar button only.

## 1.12.1

<!-- update
level: optional
en: Alt + I / D / V / O open the images, documents, video or any-files donut
en: macOS: disk image (.dmg), easier install and updates
-->

- New two-key shortcuts: select files and press Alt (Option on macOS) + **I** for the images donut, **D** documents,
  **V** video or **O** any files. With a mixed selection only the files of that type are used. Change the keys, the
  donut each one opens, or add your own with **Add keyboard shortcut** on the Shortcuts page.

- macOS: each release also comes as a disk image (`.dmg`): open it and drag DropMorph into Applications.
- One command installs or updates DropMorph: `curl -fsSL https://dropmorph.vercel.app/install.sh | sh` on macOS and
  Linux, `irm https://dropmorph.vercel.app/install.ps1 | iex` in Windows PowerShell. It picks the right package and
  checks its checksum.
- macOS and Linux: running `install.sh` from a newer package now updates DropMorph in place, keeping settings and
  engines. On macOS it removes the download quarantine, so the first start isn't blocked, and opens DropMorph.
- macOS: Finder's "Convert with DropMorph" menu item is translated into Hindi, Spanish and Polish too, and macOS
  explains why DropMorph asks for access to the Desktop, Documents and Downloads folders.
- Linux: `install.sh` warns about missing system libraries and prints the command that installs them.
- New guides: [macOS](docs/MACOS.md) and [Linux](docs/LINUX.md).

## 1.12.0

<!-- update
level: optional
en: First DropMorph release: new name, new look
-->

- New name and look: DropMorph, with a teal and coral theme and a new logo.
- Drag files with Ctrl held and drop them on a format in the donut at your cursor. 254 formats, fully offline.
- Updates now come from https://github.com/cod4nitish/DropMorph/releases.
