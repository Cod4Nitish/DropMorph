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

## 1.12.0

<!-- update
level: optional
en: First DropMorph release: new name, new look
-->

- New name and look: DropMorph, with a teal and coral theme and a new logo.
- Drag files with Ctrl held and drop them on a format in the donut at your cursor. 254 formats, fully offline.
- Updates now come from https://github.com/cod4nitish/DropMorph/releases.
