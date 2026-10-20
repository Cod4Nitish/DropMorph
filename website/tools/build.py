"""Builds the DropMorph website pages: home, formats, command line, the convert/* landing pages,
sitemap.xml and robots.txt. The formats table is read from the repository README so the site and
the app stay in step.

Run from anywhere:  python website/tools/build.py
"""

import html
import json
import re
from pathlib import Path

SITE = Path(__file__).resolve().parent.parent
REPO = SITE.parent
BASE = "https://dropmorph.vercel.app"
GH = "https://github.com/Cod4Nitish/DropMorph"
VERSION = "1.12.0"
RELEASED = "2026-10-20"
SETUP_URL = f"{GH}/releases/latest/download/DropMorph-{VERSION}-win-Setup.exe"
PORTABLE_URL = f"{GH}/releases/latest/download/DropMorph-{VERSION}-win-Portable.zip"
MAC_ARM_URL = f"{GH}/releases/latest/download/DropMorph-{VERSION}-osx-arm64.tar.gz"
MAC_INTEL_URL = f"{GH}/releases/latest/download/DropMorph-{VERSION}-osx-x64.tar.gz"
LINUX_X64_URL = f"{GH}/releases/latest/download/DropMorph-{VERSION}-linux-x64.tar.gz"
LINUX_ARM_URL = f"{GH}/releases/latest/download/DropMorph-{VERSION}-linux-arm64.tar.gz"
INSTALL_SH = f"curl -fsSL {BASE}/install.sh | sh"
INSTALL_PS1 = f"irm {BASE}/install.ps1 | iex"
FORMAT_COUNT = 254

ENGINES = {"¹": "Ghostscript", "²": "LibreOffice", "³": "Pandoc", "⁴": "calibre", "⁵": "FFmpeg"}
ENGINE_INFO = [
    ("FFmpeg", "~100 MB", "All video and audio: MP4, MKV, MOV, WEBM, MP3, WAV, FLAC…, GIF from video, subtitles"),
    ("LibreOffice", "~420 MB", "Older and rare office files: DOC, XLS, PPT, RTF, OpenDocument, Pages, Keynote, Numbers"),
    ("Pandoc", "~42 MB", "Markdown and markup: LaTeX, reStructuredText, AsciiDoc, Org, Jupyter…"),
    ("Ghostscript", "~20 MB", "EPS and PostScript, PostScript-based AI files"),
    ("calibre", "~216 MB", "Rare e-book formats, and EPUB → MOBI / AZW3"),
]


def esc(s):
    return html.escape(s, quote=True)


# ─────────────────────────── icons ───────────────────────────

def icon(name):
    paths = {
        "donut": '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="3.5"/><path d="M12 3v5.5M21 12h-5.5M12 21v-5.5M3 12h5.5"/>',
        "list": '<path d="M4 7h16M4 12h16M4 17h10"/>',
        "office": '<rect x="4" y="3" width="16" height="18" rx="2.5"/><path d="M8 8h8M8 12h8M8 16h5"/>',
        "stack": '<rect x="3" y="6" width="14" height="14" rx="2.5"/><path d="M7 3h11a3 3 0 0 1 3 3v11"/>',
        "lock": '<rect x="4" y="10" width="16" height="11" rx="2.5"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/>',
        "cursor": '<path d="M5 3l14 7-6 2-2 6z"/>',
        "sliders": '<path d="M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M20 18h0"/><circle cx="16" cy="6" r="2"/><circle cx="10" cy="12" r="2"/><circle cx="18" cy="18" r="2"/>',
        "bolt": '<path d="M13 2 4 14h7l-1 8 9-12h-7z"/>',
        "heart": '<path d="M12 20s-7-4.4-9-9a5 5 0 0 1 9-3 5 5 0 0 1 9 3c-2 4.6-9 9-9 9z"/>',
        "folder": '<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>',
        "palette": '<path d="M12 3a9 9 0 1 0 0 18c1.5 0 2-1 2-2s-1-1.5-1-2.5 1-1.5 2-1.5h2a4 4 0 0 0 4-4c0-4.4-4-8-9-8z"/><circle cx="7.5" cy="11" r="1"/><circle cx="10" cy="7" r="1"/><circle cx="15" cy="7" r="1"/>',
        "globe": '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c3 3.5 3 14.5 0 18M12 3c-3 3.5-3 14.5 0 18"/>',
        "download": '<path d="M12 3v12m0 0-5-5m5 5 5-5M4 21h16"/>',
        "check": '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
    }
    return f'<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">{paths[name]}</svg>'


def tick():
    return f'<span class="tick"><svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg></span>'


def gh_icon():
    return ('<svg viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8z"/></svg>')


# OS marks for the download cards (Apple and Linux from Simple Icons, CC0); fill follows currentColor.
OS_LOGOS = {
    "android": '<path d="M6.2 9.2h11.6a1 1 0 0 1 1 1v7.3a1.6 1.6 0 0 1-1.6 1.6h-.9v2.4a1.3 1.3 0 0 1-2.6 0v-2.4h-3.4v2.4a1.3 1.3 0 0 1-2.6 0v-2.4h-.9a1.6 1.6 0 0 1-1.6-1.6v-7.3a1 1 0 0 1 1-1zM3.4 9.6a1.3 1.3 0 0 1 1.3 1.3v5.2a1.3 1.3 0 0 1-2.6 0v-5.2a1.3 1.3 0 0 1 1.3-1.3zm17.2 0a1.3 1.3 0 0 1 1.3 1.3v5.2a1.3 1.3 0 0 1-2.6 0v-5.2a1.3 1.3 0 0 1 1.3-1.3zM16.3 2.1l-1.2 2.1A6.2 6.2 0 0 1 18.6 8H5.4a6.2 6.2 0 0 1 3.5-3.8L7.7 2.1a.3.3 0 0 1 .5-.3l1.3 2.1a6.8 6.8 0 0 1 5 0l1.3-2.1a.3.3 0 0 1 .5.3zM9.3 5.6a.7.7 0 1 0 0 1.4.7.7 0 0 0 0-1.4zm5.4 0a.7.7 0 1 0 0 1.4.7.7 0 0 0 0-1.4z"/>',
    "windows": '<rect x="1" y="1" width="10.5" height="10.5"/><rect x="12.5" y="1" width="10.5" height="10.5"/><rect x="1" y="12.5" width="10.5" height="10.5"/><rect x="12.5" y="12.5" width="10.5" height="10.5"/>',
    "mac": '<path d="M12.152 6.896c-.948 0-2.415-1.078-3.96-1.04-2.04.027-3.91 1.183-4.961 3.014-2.117 3.675-.546 9.103 1.519 12.09 1.013 1.454 2.208 3.09 3.792 3.039 1.52-.065 2.09-.987 3.935-.987 1.831 0 2.35.987 3.96.948 1.637-.026 2.676-1.48 3.676-2.948 1.156-1.688 1.636-3.325 1.662-3.415-.039-.013-3.182-1.221-3.22-4.857-.026-3.04 2.48-4.494 2.597-4.559-1.429-2.09-3.623-2.324-4.39-2.376-2-.156-3.675 1.09-4.61 1.09zM15.53 3.83c.843-1.012 1.4-2.427 1.245-3.83-1.207.052-2.662.805-3.532 1.818-.78.896-1.454 2.338-1.273 3.714 1.338.104 2.715-.688 3.559-1.701"/>',
    "linux": '<path d="M12.504 0c-.155 0-.315.008-.48.021-4.226.333-3.105 4.807-3.17 6.298-.076 1.092-.3 1.953-1.05 3.02-.885 1.051-2.127 2.75-2.716 4.521-.278.832-.41 1.684-.287 2.489a.424.424 0 00-.11.135c-.26.268-.45.6-.663.839-.199.199-.485.267-.797.4-.313.136-.658.269-.864.68-.09.189-.136.394-.132.602 0 .199.027.4.055.536.058.399.116.728.04.97-.249.68-.28 1.145-.106 1.484.174.334.535.47.94.601.81.2 1.91.135 2.774.6.926.466 1.866.67 2.616.47.526-.116.97-.464 1.208-.946.587-.003 1.23-.269 2.26-.334.699-.058 1.574.267 2.577.2.025.134.063.198.114.333l.003.003c.391.778 1.113 1.132 1.884 1.071.771-.06 1.592-.536 2.257-1.306.631-.765 1.683-1.084 2.378-1.503.348-.199.629-.469.649-.853.023-.4-.2-.811-.714-1.376v-.097l-.003-.003c-.17-.2-.25-.535-.338-.926-.085-.401-.182-.786-.492-1.046h-.003c-.059-.054-.123-.067-.188-.135a.357.357 0 00-.19-.064c.431-1.278.264-2.55-.173-3.694-.533-1.41-1.465-2.638-2.175-3.483-.796-1.005-1.576-1.957-1.56-3.368.026-2.152.236-6.133-3.544-6.139zm.529 3.405h.013c.213 0 .396.062.584.198.19.135.33.332.438.533.105.259.158.459.166.724 0-.02.006-.04.006-.06v.105a.086.086 0 01-.004-.021l-.004-.024a1.807 1.807 0 01-.15.706.953.953 0 01-.213.335.71.71 0 00-.088-.042c-.104-.045-.198-.064-.284-.133a1.312 1.312 0 00-.22-.066c.05-.06.146-.133.183-.198.053-.128.082-.264.088-.402v-.02a1.21 1.21 0 00-.061-.4c-.045-.134-.101-.2-.183-.333-.084-.066-.167-.132-.267-.132h-.016c-.093 0-.176.03-.262.132a.8.8 0 00-.205.334 1.18 1.18 0 00-.09.4v.019c.002.089.008.179.02.267-.193-.067-.438-.135-.607-.202a1.635 1.635 0 01-.018-.2v-.02a1.772 1.772 0 01.15-.768c.082-.22.232-.406.43-.533a.985.985 0 01.594-.2zm-2.962.059h.036c.142 0 .27.048.399.135.146.129.264.288.344.465.09.199.14.4.153.667v.004c.007.134.006.2-.002.266v.08c-.03.007-.056.018-.083.024-.152.055-.274.135-.393.2.012-.09.013-.18.003-.267v-.015c-.012-.133-.04-.2-.082-.333a.613.613 0 00-.166-.267.248.248 0 00-.183-.064h-.021c-.071.006-.13.04-.186.132a.552.552 0 00-.12.27.944.944 0 00-.023.33v.015c.012.135.037.2.08.334.046.134.098.2.166.268.01.009.02.018.034.024-.07.057-.117.07-.176.136a.304.304 0 01-.131.068 2.62 2.62 0 01-.275-.402 1.772 1.772 0 01-.155-.667 1.759 1.759 0 01.08-.668 1.43 1.43 0 01.283-.535c.128-.133.26-.2.418-.2zm1.37 1.706c.332 0 .733.065 1.216.399.293.2.523.269 1.052.468h.003c.255.136.405.266.478.399v-.131a.571.571 0 01.016.47c-.123.31-.516.643-1.063.842v.002c-.268.135-.501.333-.775.465-.276.135-.588.292-1.012.267a1.139 1.139 0 01-.448-.067 3.566 3.566 0 01-.322-.198c-.195-.135-.363-.332-.612-.465v-.005h-.005c-.4-.246-.616-.512-.686-.71-.07-.268-.005-.47.193-.6.224-.135.38-.271.483-.336.104-.074.143-.102.176-.131h.002v-.003c.169-.202.436-.47.839-.601.139-.036.294-.065.466-.065zm2.8 2.142c.358 1.417 1.196 3.475 1.735 4.473.286.534.855 1.659 1.102 3.024.156-.005.33.018.513.064.646-1.671-.546-3.467-1.089-3.966-.22-.2-.232-.335-.123-.335.59.534 1.365 1.572 1.646 2.757.13.535.16 1.104.021 1.67.067.028.135.06.205.067 1.032.534 1.413.938 1.23 1.537v-.043c-.06-.003-.12 0-.18 0h-.016c.151-.467-.182-.825-1.065-1.224-.915-.4-1.646-.336-1.77.465-.008.043-.013.066-.018.135-.068.023-.139.053-.209.064-.43.268-.662.669-.793 1.187-.13.533-.17 1.156-.205 1.869v.003c-.02.334-.17.838-.319 1.35-1.5 1.072-3.58 1.538-5.348.334a2.645 2.645 0 00-.402-.533 1.45 1.45 0 00-.275-.333c.182 0 .338-.03.465-.067a.615.615 0 00.314-.334c.108-.267 0-.697-.345-1.163-.345-.467-.931-.995-1.788-1.521-.63-.4-.986-.87-1.15-1.396-.165-.534-.143-1.085-.015-1.645.245-1.07.873-2.11 1.274-2.763.107-.065.037.135-.408.974-.396.751-1.14 2.497-.122 3.854a8.123 8.123 0 01.647-2.876c.564-1.278 1.743-3.504 1.836-5.268.048.036.217.135.289.202.218.133.38.333.59.465.21.201.477.335.876.335.039.003.075.006.11.006.412 0 .73-.134.997-.268.29-.134.52-.334.74-.4h.005c.467-.135.835-.402 1.044-.7zm2.185 8.958c.037.6.343 1.245.882 1.377.588.134 1.434-.333 1.791-.765l.211-.01c.315-.007.577.01.847.268l.003.003c.208.199.305.53.391.876.085.4.154.78.409 1.066.486.527.645.906.636 1.14l.003-.007v.018l-.003-.012c-.015.262-.185.396-.498.595-.63.401-1.746.712-2.457 1.57-.618.737-1.37 1.14-2.036 1.191-.664.053-1.237-.2-1.574-.898l-.005-.003c-.21-.4-.12-1.025.056-1.69.176-.668.428-1.344.463-1.897.037-.714.076-1.335.195-1.814.12-.465.308-.797.641-.984l.045-.022zm-10.814.049h.01c.053 0 .105.005.157.014.376.055.706.333 1.023.752l.91 1.664.003.003c.243.533.754 1.064 1.189 1.637.434.598.77 1.131.729 1.57v.006c-.057.744-.48 1.148-1.125 1.294-.645.135-1.52.002-2.395-.464-.968-.536-2.118-.469-2.857-.602-.369-.066-.61-.2-.723-.4-.11-.2-.113-.602.123-1.23v-.004l.002-.003c.117-.334.03-.752-.027-1.118-.055-.401-.083-.71.043-.94.16-.334.396-.4.69-.533.294-.135.64-.202.915-.47h.002v-.002c.256-.268.445-.601.668-.838.19-.201.38-.336.663-.336zm7.159-9.074c-.435.201-.945.535-1.488.535-.542 0-.97-.267-1.28-.466-.154-.134-.28-.268-.373-.335-.164-.134-.144-.333-.074-.333.109.016.129.134.199.2.096.066.215.2.36.333.292.2.68.467 1.167.467.485 0 1.053-.267 1.398-.466.195-.135.445-.334.648-.467.156-.136.149-.267.279-.267.128.016.034.134-.147.332a8.097 8.097 0 01-.69.468zm-1.082-1.583V5.64c-.006-.02.013-.042.029-.05.074-.043.18-.027.26.004.063 0 .16.067.15.135-.006.049-.085.066-.135.066-.055 0-.092-.043-.141-.068-.052-.018-.146-.008-.163-.065zm-.551 0c-.02.058-.113.049-.166.066-.047.025-.086.068-.14.068-.05 0-.13-.02-.136-.068-.01-.066.088-.133.15-.133.08-.031.184-.047.259-.005.019.009.036.03.03.05v.02h.003z"/>',
}


def os_logo(name):
    return f'<svg class="os-logo" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">{OS_LOGOS[name]}</svg>'


def star_card():
    return (f'<div class="star-card"><div class="icon">{gh_icon()}</div><p><b>Like DropMorph? Help the developer: give it a ⭐ on GitHub.</b>'
            f'<span>DropMorph is free. A star takes one click and helps other people find it.</span></p>'
            f'<a class="btn btn-primary" href="{GH}">{gh_icon()} Star on GitHub<span class="stars" data-stars hidden></span></a></div>')


# site.js points these at the visitor's computer (Mac, Linux, phone → download section); without JS they stay Windows.
def dl_btn():
    return (f'<a class="btn btn-primary" href="{SETUP_URL}" data-dl data-arm="{MAC_ARM_URL}" data-intel="{MAC_INTEL_URL}">'
            f'{icon("download")} <span>Download for Windows</span></a>')


def guide_btn(label="Install guide"):
    return f'<a class="btn btn-ghost" href="/install/" data-guide>{label}</a>'


def cmd_box(label, cmd):
    return (f'<div class="cmd-box"><span>{label}</span><div class="cmd">'
            f'<pre tabindex="0" aria-label="Install command">{esc(cmd)}</pre><button type="button" data-copy>Copy</button></div></div>')


# ─────────────────────────── layout ───────────────────────────

NAV = [("How it works", "/#how"), ("Features", "/#features"), ("Formats", "/formats/"),
       ("Command line", "/cli/"), ("Install guide", "/install/"), ("FAQ", "/#faq")]


def page(path, title, desc, body, active=None, jsonld=(), extra_head=""):
    canonical = BASE + path
    nav = "".join(f'<a href="{href}"{" aria-current=\"page\"" if label == active else ""}>{label}</a>' for label, href in NAV)
    ld = "".join(f'<script type="application/ld+json">{json.dumps(j, ensure_ascii=False)}</script>' for j in jsonld)
    popular = "".join(f'<a href="/convert/{c["slug"]}/">{esc(c["from"])} → {esc(c["to"])}</a>' for c in CONVERSIONS[:6])
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{esc(title)}</title>
<meta name="description" content="{esc(desc)}">
<link rel="canonical" href="{canonical}">
<meta property="og:type" content="website">
<meta property="og:url" content="{canonical}">
<meta property="og:title" content="{esc(title)}">
<meta property="og:description" content="{esc(desc)}">
<meta property="og:image" content="{BASE}/og.png">
<meta name="twitter:card" content="summary_large_image">
<meta name="theme-color" content="#0D9488">
<link rel="icon" href="/logo.svg" type="image/svg+xml">
<link rel="apple-touch-icon" href="/logo.png">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
<link rel="stylesheet" href="/assets/site.css">
{extra_head}{ld}
</head>
<body>
<a class="skip" href="#main">Skip to content</a>
<nav class="top" aria-label="Main">
  <div class="wrap">
    <a class="brand" href="/"><img src="/logo.svg" alt="" width="32" height="32"><span>DropMorph</span></a>
    <div class="navlinks">{nav}</div>
    <button class="theme-toggle" id="themeToggle" type="button" aria-label="Switch light or dark theme">◐</button>
    <a class="btn btn-line btn-small nav-star" href="{GH}" aria-label="Star DropMorph on GitHub">{gh_icon()}<span>Star</span><span class="stars" data-stars hidden></span></a>
    <a class="btn btn-primary btn-small nav-dl" href="/#download">{icon("download")}Download</a>
  </div>
</nav>
<main id="main">
{body}
</main>
<footer class="site">
  <div class="wrap">
    <div class="foot-grid">
      <div>
        <a class="brand" href="/" style="font-size:17px"><img src="/logo.svg" alt="" width="28" height="28">DropMorph</a>
        <p style="margin:12px 0 0;max-width:300px">A free, offline file converter you use by dragging. {FORMAT_COUNT} formats, nothing uploaded.</p>
      </div>
      <div><h4>Product</h4><a href="/#features">Features</a><a href="/formats/">All formats</a><a href="/cli/">Command line</a><a href="/#download">Download</a><a href="/install/">Windows install guide</a><a href="/install/mac/">Mac install guide</a><a href="/install/linux/">Linux install guide</a><a href="/#faq">FAQ</a></div>
      <div><h4>Popular</h4>{popular}</div>
      <div><h4>Project</h4><a href="{GH}">GitHub</a><a href="{GH}/releases">All releases</a><a href="{GH}/issues">Report an issue</a><a href="{GH}/blob/main/LICENSE">MIT License</a><a href="{GH}/blob/main/THIRD-PARTY-NOTICES.md">Third-party notices</a></div>
    </div>
    <div class="foot-bottom"><span>© 2026 DropMorph · Developed by <a class="dev-name" href="https://github.com/cod4nitish">Nitish Singh</a></span><span>Version {VERSION}</span></div>
  </div>
</footer>
<script src="/assets/site.js" defer></script>
</body>
</html>
"""


def write(rel, text):
    out = SITE / rel
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8", newline="\n")


# ─────────────────────────── formats (from README) ───────────────────────────

def read_formats():
    readme = (REPO / "README.md").read_text(encoding="utf-8")
    block = readme[readme.index("## Formats"):readme.index("## Command line")]
    rows = []
    for line in block.splitlines():
        if not line.startswith("| **"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        src, dst = cells[0], cells[1]
        # "**Video**⁵: MP4, …" – a footnote after the name applies to the whole group
        m = re.match(r"\*\*(.+?)\*\*([¹²³⁴⁵]*)(?::\s*(.*))?$", src)
        name, note, src_list = m.group(1), m.group(2), (m.group(3) or m.group(1))
        if note:
            src_list = f"{src_list} {note}"
        rows.append((name, src_list, dst))
    return rows


def fmt_html(text):
    text = esc(text.replace("**", ""))
    for sup, engine in ENGINES.items():
        text = text.replace(sup, f'<span class="eng" title="Needs the optional {engine} engine">{engine}</span>')
    return text


# ─────────────────────────── conversions ───────────────────────────

CONVERSIONS = [
    {"slug": "heic-to-jpg", "from": "HEIC", "to": "JPG", "engine": None,
     "title": "Convert HEIC to JPG on Windows, offline and free",
     "intro": "iPhone photos arrive as HEIC, and plenty of apps, websites and office tools still won't open them. DropMorph turns them into ordinary JPGs on your own PC, one photo or a whole folder at once.",
     "drop": "JPG", "points": ["Keeps the picture upright and in sRGB colour", "Pick the JPG quality in the preset, or resize while converting", "Hundreds of photos at once, in parallel"],
     "faq": [("Will the photo lose quality?", "JPG is compressed, so set the preset's quality to 90–95 for photos you want to keep. The default is a good balance of size and quality."),
             ("Do I need any extra software?", "No. HEIC to JPG works out of the box, without an engine download.")]},
    {"slug": "pdf-to-word", "from": "PDF", "to": "Word", "engine": None,
     "title": "Convert PDF to Word (DOCX) offline, free",
     "intro": "Need to edit a PDF? DropMorph turns it into a Word document you can change in Word, WPS or Google Docs, without uploading a contract or salary slip to a website.",
     "drop": "DOCX", "points": ["Text, paragraphs and pictures come across as editable content", "Works without Microsoft Office installed", "Convert a stack of PDFs in one go"],
     "faq": [("Will it look exactly like the PDF?", "Text and pictures come across; complex layouts may need small fixes in Word, as with any PDF-to-Word converter."),
             ("Does it work for scanned PDFs?", "A scanned page is a picture, so it comes across as a picture rather than editable text.")]},
    {"slug": "word-to-pdf", "from": "Word", "to": "PDF", "engine": None,
     "title": "Convert Word (DOCX) to PDF offline, free",
     "intro": "Send documents that look the same on every device. DropMorph saves DOCX files as PDF without Microsoft Office, and without uploading them anywhere.",
     "drop": "PDF", "points": ["No Office or LibreOffice needed for DOCX", "Many documents at once", "Older .DOC files work too with the optional LibreOffice engine"],
     "faq": [("Can I convert old .doc files?", "Yes, with the optional LibreOffice engine, which DropMorph downloads when you first need it."),
             ("Where is the PDF saved?", "Next to the original document, unless you pick another output folder in the preset.")]},
    {"slug": "excel-to-pdf", "from": "Excel", "to": "PDF", "engine": None,
     "title": "Convert Excel (XLSX) to PDF offline, free",
     "intro": "Turn spreadsheets and reports into PDFs you can email or print. DropMorph converts XLSX on your own computer, so financial sheets never leave it.",
     "drop": "PDF", "points": ["XLSX, XLSM and CSV work out of the box", "Also Excel → CSV, JSON or HTML", "Older .XLS files with the optional LibreOffice engine"],
     "faq": [("Does it need Excel installed?", "No. DropMorph reads XLSX itself."),
             ("Can I convert to CSV instead?", "Yes. Drop the file on CSV, TSV, JSON or HTML in the donut.")]},
    {"slug": "jpg-to-pdf", "from": "JPG", "to": "PDF", "engine": None,
     "title": "Convert JPG to PDF offline, free",
     "intro": "Scanned bills, ID photos or screenshots: DropMorph turns pictures into PDFs, and can put many pictures into one PDF with the Merge preset.",
     "drop": "PDF", "points": ["Any image: JPG, PNG, HEIC, WEBP, TIFF…", "One PDF per picture, or all pictures merged into one", "Resize or switch to grayscale while converting"],
     "faq": [("How do I put several photos into one PDF?", "Select them all, drag them with Ctrl held and drop them on Merge PDF."),
             ("Is there a file limit?", "No limits. Everything runs on your PC.")]},
    {"slug": "pdf-to-jpg", "from": "PDF", "to": "JPG", "engine": None,
     "title": "Convert PDF to JPG (every page) offline, free",
     "intro": "Share a PDF page on WhatsApp or put it in a slide. DropMorph saves every page of a PDF as a JPG or PNG picture, at the resolution you choose.",
     "drop": "JPG", "points": ["Every page, or a page range from the preset", "Pick the DPI for sharp pictures", "PNG, TIFF and other image formats too"],
     "faq": [("Can I convert only some pages?", "Yes. Set a page range in the PDF preset."),
             ("How sharp are the pictures?", "Set the DPI in the preset; 150 is good for screens and 300 for print.")]},
    {"slug": "compress-pdf", "from": "PDF", "to": "smaller PDF", "engine": None,
     "title": "Compress PDF offline: make PDFs smaller for email",
     "intro": "Portal upload limit or email attachment too big? DropMorph's Compress PDF preset scales down the pictures inside a PDF and keeps text and drawings sharp.",
     "drop": "Compress PDF", "points": ["Smallest (72 dpi), Balanced (150 dpi) or High quality (300 dpi)", "Text, fonts and drawings stay as they are", "If the result isn't smaller, the original is kept"],
     "faq": [("Will the text get blurry?", "No. Only pictures are scaled down and saved as JPEG; text and vector drawings are untouched."),
             ("How much smaller will it get?", "It depends on how many pictures the PDF has. Scanned PDFs shrink the most.")]},
    {"slug": "merge-pdf", "from": "PDF", "to": "one PDF", "engine": None,
     "title": "Merge PDF files offline, free",
     "intro": "Combine invoices, certificates or scanned pages into a single PDF. Select the files, drag and drop them on Merge PDF. That's it.",
     "drop": "Merge PDF", "points": ["Merge PDFs and pictures together", "Split PDF does the opposite: one file per page", "Nothing is uploaded, so it's safe for documents like ID proofs"],
     "faq": [("In which order are the files merged?", "In the order of the selection you drag."),
             ("Can I split a PDF too?", "Yes. Drop it on Split PDF to get one file per page.")]},
    {"slug": "mp4-to-mp3", "from": "MP4", "to": "MP3", "engine": "FFmpeg",
     "title": "Convert MP4 to MP3 offline, free",
     "intro": "Keep just the audio of a video: a meeting recording, a lecture or a song. DropMorph extracts it as MP3, M4A, WAV or FLAC on your PC.",
     "drop": "MP3", "points": ["MP4, MKV, MOV, WEBM, AVI and more", "Also video → smaller MP4, GIF or 720p", "Uses the optional FFmpeg engine (~100 MB, downloaded once)"],
     "faq": [("Why does it ask to download FFmpeg?", "Video and audio use the FFmpeg engine. DropMorph downloads it once, from its official source, the first time you convert a video."),
             ("Can I convert many videos at once?", "Yes. Select them all and drop them on MP3.")]},
    {"slug": "webp-to-png", "from": "WEBP", "to": "PNG", "engine": None,
     "title": "Convert WEBP to PNG or JPG offline, free",
     "intro": "Pictures saved from websites often come as WEBP, which many programs can't open. DropMorph turns them into PNG or JPG in one drag.",
     "drop": "PNG", "points": ["Transparency is kept in PNG", "Also AVIF, JPEG XL, GIF and 30+ image formats", "Whole folders at once"],
     "faq": [("PNG or JPG?", "PNG keeps transparency and is lossless; JPG is smaller for photos."),
             ("Does it work offline?", "Yes, completely.")]},
]


def conv_page(c):
    eng_note = (f'<div class="tip"><b>Needs the {c["engine"]} engine.</b> DropMorph downloads it once, the first time you need it, from its official source.</div>'
                if c["engine"] else '<div class="tip"><b>Works out of the box.</b> No extra software or engine download needed.</div>')
    related = "".join(f'<a class="pill-link" href="/convert/{o["slug"]}/">{esc(o["from"])} <i>→</i> {esc(o["to"])}</a>' for o in CONVERSIONS if o is not c)
    points = "".join(f'<div>{tick()}<p style="margin:0"><b>{esc(p)}</b></p></div>' for p in c["points"])
    faq_items = c["faq"] + [("Are my files uploaded?", "No. DropMorph converts everything on your own computer and works offline."),
                            ("Is DropMorph free?", "Yes. Free and open source, with no sign-up, watermark or limits.")]
    faq = "".join(f"<details{' open' if i == 0 else ''}><summary>{esc(q)}</summary><p>{esc(a)}</p></details>" for i, (q, a) in enumerate(faq_items))
    body = f"""
<header class="hero"><div class="wrap hero-simple">
  <div class="crumbs"><a href="/">DropMorph</a> › <a href="/formats/">Formats</a> › {esc(c["from"])} to {esc(c["to"])}</div>
  <h1>{esc(c["from"])} <span>→</span> {esc(c["to"])}</h1>
  <p class="lead">{esc(c["intro"])}</p>
  <div class="cta">{dl_btn()}{guide_btn()}
  <small>Free · Offline · Windows 10 &amp; 11 · macOS 14+ · Linux</small></div>
</div></header>

<section class="sec"><div class="wrap">
  <div class="sec-head"><div class="kicker">How to</div><h2>{esc(c["from"])} to {esc(c["to"])} in three steps</h2></div>
  <div class="steps">
    <div class="stepc"><div class="n">1</div><h3>Hold <kbd>Ctrl</kbd> and drag</h3><p>Select your {esc(c["from"])} file (or many), hold Ctrl (Option on macOS) and start dragging.</p></div>
    <div class="stepc"><div class="n">2</div><h3>Drop on {esc(c["drop"])}</h3><p>A donut of formats opens at your cursor. Drop the file on <b>{esc(c["drop"])}</b>.</p></div>
    <div class="stepc"><div class="n">3</div><h3>Done</h3><p>The new file is saved right next to the original. Prefer menus? Right-click the file → DropMorph.</p></div>
  </div>
  {eng_note}
  <div class="feat-list">{points}</div>
</div></section>

<section class="sec alt"><div class="wrap">
  <div class="sec-head"><div class="kicker">Why offline</div><h2>Your files stay on your PC</h2></div>
  <div class="vs">
    <div class="card"><h3>Online converter sites</h3><ul>
      <li><span class="bad">✕</span><span>You <b>upload</b> the file to someone else's server</span></li>
      <li><span class="bad">✕</span><span>Ads, waiting, and daily or size limits</span></li>
      <li><span class="bad">✕</span><span>One file at a time, needs internet</span></li></ul></div>
    <div class="card"><h3>DropMorph</h3><ul>
      <li><span class="good">✓</span><span>Converts <b>on your computer</b>, nothing uploaded</span></li>
      <li><span class="good">✓</span><span>No ads, no sign-up, <b>no limits</b></span></li>
      <li><span class="good">✓</span><span>Many files at once, <b>works offline</b></span></li></ul></div>
  </div>
</div></section>

<section class="sec"><div class="wrap narrow">
  <div class="sec-head"><div class="kicker">FAQ</div><h2>{esc(c["from"])} to {esc(c["to"])}: questions</h2></div>
  {faq}
  <h3 style="margin:40px 0 14px">More conversions</h3>
  <div class="pills">{related}</div>
</div></section>
{final_cta()}
"""
    ld = [{"@context": "https://schema.org", "@type": "HowTo", "name": c["title"],
           "step": [{"@type": "HowToStep", "text": f"Hold Ctrl and drag the {c['from']} file."},
                    {"@type": "HowToStep", "text": f"Drop it on {c['drop']} in the donut that opens at the cursor."},
                    {"@type": "HowToStep", "text": "The converted file is saved next to the original."}]},
          faq_ld(faq_items)]
    desc = f"{c['intro'][:150].rsplit(' ', 1)[0]}… Free, offline, no upload."
    return page(f"/convert/{c['slug']}/", f"{c['title']} | DropMorph", desc, body, jsonld=ld)


def final_cta():
    return f"""<section class="sec" style="padding-top:0"><div class="final">
  <h2>Stop uploading files to convert them.</h2>
  <p>Drag. Drop. Morphed. Free for Windows, macOS and Linux.</p>
  <div class="cta" style="justify-content:center">{dl_btn()}{guide_btn()}<a class="btn btn-ghost" href="/#download">All downloads</a></div>
  {star_card()}
</div></section>"""


def faq_ld(items):
    return {"@context": "https://schema.org", "@type": "FAQPage",
            "mainEntity": [{"@type": "Question", "name": q, "acceptedAnswer": {"@type": "Answer", "text": a}} for q, a in items]}


# ─────────────────────────── home ───────────────────────────

HOME_FAQ = [
    ("Is DropMorph really free?", "Yes. DropMorph is free and open source under the MIT license. There's no account, no watermark, no trial and no limit on files."),
    ("Does it work offline? Are my files uploaded?", "Everything is converted on your own computer and nothing is uploaded. DropMorph only goes online to check GitHub for a newer version and to download an optional engine you choose."),
    (f"Which file formats does DropMorph support?", f"{FORMAT_COUNT} formats in 18 groups: images, PDF, Word, Excel, PowerPoint, e-books, video, audio, subtitles, archives, CAD, fonts, data files, 3D models and map data. See the full list on the Formats page."),
    ("Do I need Microsoft Office or LibreOffice?", "No. DOCX, XLSX, PPTX, PDF and most other formats work out of the box. Only older and rare formats such as DOC, PPT and Pages use the optional LibreOffice engine."),
    ("What are the optional engines?", "Free programs DropMorph downloads only when you need them: FFmpeg for video and audio, LibreOffice for older office files, Pandoc for markup, Ghostscript for PostScript and calibre for rare e-books. Each is pinned to an official download and checked before use."),
    ("Does installing need administrator rights?", "The Windows Setup asks for administrator permission once, and the portable ZIP runs without installing. On macOS and Linux DropMorph installs into your own user folder, without an administrator password."),
    ("Can I convert many files at once?", "Yes. Select as many files as you like and drop them together; they convert in parallel. Watch folders convert every file that lands in them."),
    ("Can I convert from the Windows 11 right-click menu?", "Yes. Setup can add DropMorph to the right-click menu, including the top of the compact Windows 11 menu."),
    ("Can it convert from the command line or automatically?", "Yes. The dropmorph command converts files and folders, and dropmorph watch converts everything that lands in a folder. See the Command line page."),
    ("How do I convert a file, for example HEIC to JPG?", "Hold Ctrl, drag the HEIC photo and drop it on JPG in the donut. The JPG is saved next to the original."),
    ("Why use DropMorph instead of an online converter?", "Online converters upload your files to someone else's server, show ads and limit file sizes. DropMorph is faster, private, has no limits and works without internet."),
    ("Which operating systems are supported?", "Windows 10 (1809 or newer) and Windows 11; macOS 14 Sonoma or newer, on Apple silicon (M1–M4) and Intel Macs; and Linux (Ubuntu 22.04 or newer and similar), on x64 and ARM64."),
    ("Can I use it on my phone?", "DropMorph is a desktop app for Windows, macOS and Linux; it doesn't run on Android or iPhone. You can open this site on your phone and share the download link to your computer."),
]

FEATURES = [
    ("donut", "", "A donut at your cursor", "Hold Ctrl and drag. The formats open right where you are, and only the ones that fit the files you're holding."),
    ("list", "coral", f"{FORMAT_COUNT} formats, one gesture", "Images, PDF, office files, e-books, video, audio, archives, fonts, CAD, 3D and map data, all with the same drag."),
    ("office", "", "No Office needed", "Word, Excel and PowerPoint files convert without Microsoft Office, WPS or LibreOffice installed."),
    ("stack", "coral", "Batch conversion", "Drop a hundred files at once and they convert in parallel. Multi-step routes are found automatically."),
    ("lock", "", "100% on your PC", "Nothing is uploaded. No account, no telemetry. Safe for contracts, salary slips and ID proofs."),
    ("cursor", "coral", "Right where you work", "The donut, the right-click menu, a keyboard shortcut, the drop zone, or the dropmorph command."),
    ("sliders", "", "Presets per file type", "Quality, size, DPI, page ranges, compression, video and audio settings, output folder and file names."),
    ("bolt", "coral", "Engines on demand", "Video, old Office files and rare formats use free engines that download only when you first need them."),
    ("heart", "", "Free and open source", "MIT licensed and built in public on GitHub. No trial, no watermark, no limits."),
]


def home():
    feats = "".join(f'<div class="card"><div class="icon {cls}">{icon(ic)}</div><h3>{esc(t)}</h3><p>{esc(d)}</p></div>' for ic, cls, t, d in FEATURES)
    pills = "".join(f'<a class="pill-link" href="/convert/{c["slug"]}/">{esc(c["from"])} <i>→</i> {esc(c["to"])}</a>' for c in CONVERSIONS)
    cats = read_formats()
    teaser = "".join(f'<div class="fmt"><h3>{esc(n)}</h3><p>{fmt_html(s)}</p></div>' for n, s, _ in cats[:12])
    faq = "".join(f"<details{' open' if i == 0 else ''}><summary>{esc(q)}</summary><p>{esc(a)}</p></details>" for i, (q, a) in enumerate(HOME_FAQ))
    glance = [("Price", "Free (MIT license)"), ("Version", f"{VERSION}, released {RELEASED}"),
              ("Windows", "Windows 10 (1809+) and 11 · Setup 112 MB or portable ZIP"),
              ("macOS", "macOS 14+ · Apple silicon (M1–M4) and Intel"), ("Linux", "Ubuntu 22.04+ and similar · x64 and ARM64"),
              ("Formats", f"{FORMAT_COUNT} in 18 groups"), ("Internet", "Not needed to convert. Used only for update checks and optional engines"),
              ("Languages", "English, हिन्दी, Español, 한국어, 简体中文, Polski"), ("Ways in", "Ctrl + drag donut, right-click menu, shortcut, drop zone, command line, watch folders"),
              ("Account", "None. No sign-up, no telemetry")]
    glance_rows = "".join(f"<tr><td>{esc(k)}</td><td>{esc(v)}</td></tr>" for k, v in glance)
    body = f"""
<header class="hero" id="demo">
  <div class="wrap hero-grid">
    <div>
      <span class="eyebrow">Free · Offline · <b>{FORMAT_COUNT} formats</b></span>
      <h1>Drag. Drop.<br><span>Morphed.</span></h1>
      <p class="lead">The free file converter that lives at your cursor. Hold <kbd class="kbd-hero">Ctrl</kbd>, drag a file and drop it on a format. No app window, no uploads, no waiting.</p>
      <div class="cta">
        {dl_btn()}
        {guide_btn()}
        <small>Free for Windows 10 &amp; 11, macOS 14+ and Linux · v{VERSION}</small>
        <small class="gh-line">Free &amp; open source. <a href="{GH}">⭐ Star it on GitHub</a> to help the developer.</small>
        <a class="dev-badge" href="https://github.com/cod4nitish">Developed by <b>Nitish Singh</b></a>
      </div>
    </div>
    <div class="stage" id="stage" aria-label="Interactive demo: drag the file and drop it on a format">
      <div class="stage-bar"><i></i><i></i><i></i><span>Try it: drag the file</span></div>
      <div class="picker" id="picker"></div>
      <svg class="donut" id="donut" viewBox="-140 -140 280 280"></svg>
      <svg class="ring" id="ring" viewBox="0 0 76 76"><circle class="track" cx="38" cy="38" r="32"/><circle class="bar" cx="38" cy="38" r="32"/></svg>
      <div class="hint" id="hint">Drag the file. A donut of formats opens, so drop it on one.</div>
    </div>
  </div>
</header>

<section class="sec" id="video"><div class="wrap video-grid">
  <div class="phone"><video id="reel" src="/media/dropmorph-reel.mp4" poster="/media/dropmorph-reel-poster.jpg" autoplay muted loop playsinline preload="metadata" aria-label="DropMorph demo video: Ctrl-drag a file onto the format ring and it converts"></video>
    <button class="sound" id="reelSound" type="button" aria-pressed="false">🔊 Sound on</button></div>
  <div>
    <div class="kicker">See it in action</div>
    <h2>One drag. That's the whole app.</h2>
    <p class="video-lead">Hold Ctrl, drag a photo, drop it on PDF. A progress ring, and the PDF appears next to the original. The same works for Word, Excel, videos and 250 other formats.</p>
    <div class="feat-list" style="grid-template-columns:1fr">
      <div>{tick()}<p style="margin:0"><b>No window to open</b><span>The formats come to your cursor, wherever you are.</span></p></div>
      <div>{tick()}<p style="margin:0"><b>Only formats that fit</b><span>Drag a PDF and you see PDF options; drag a video and you see video ones.</span></p></div>
      <div>{tick()}<p style="margin:0"><b>Nothing uploaded</b><span>Converted on your PC, saved next to the original.</span></p></div>
    </div>
    <p style="margin-top:22px"><a class="btn btn-line" href="#demo">Try the live demo above ↑</a></p>
  </div>
</div></section>

<section class="sec alt" id="how"><div class="wrap">
  <div class="sec-head center"><div class="kicker">How it works</div><h2>Convert files in three steps</h2><p>No window to open and no menu to dig through.</p></div>
  <div class="steps">
    <div class="stepc"><div class="n">1</div><h3>Hold a key and drag</h3><p>Hold <kbd>Ctrl</kbd> (<kbd>Option</kbd> on macOS) and start dragging one file or a whole selection, from Explorer, the desktop or any folder.</p></div>
    <div class="stepc"><div class="n">2</div><h3>Drop on a format</h3><p>A donut opens at your cursor with only the formats that fit. Drop on PDF, JPG, MP3 or any other, or on a preset like Compress PDF.</p></div>
    <div class="stepc"><div class="n">3</div><h3>Done, next to the original</h3><p>A progress ring shows the work. The result is saved beside the original, and the original is never changed.</p></div>
  </div>
</div></section>

<section class="sec" id="features"><div class="wrap">
  <div class="sec-head"><div class="kicker">What DropMorph does</div><h2>Everything a converter should do. Nothing it shouldn't.</h2><p>No uploading to some random site, no ads, no limits. The formats come to your cursor.</p></div>
  <div class="grid3">{feats}</div>
</div></section>

<section class="sec alt" id="office"><div class="wrap">
  <div class="sec-head"><div class="kicker">For everyday office work</div><h2>The conversions you do every week</h2><p>Each one is a single drag. Tap one for a step-by-step guide.</p></div>
  <div class="pills">{pills}</div>
  <div class="feat-list" style="margin-top:34px">
    <div>{tick()}<p style="margin:0"><b>Edit while converting</b><span>Crop, rotate, flip, grayscale, half size and remove background for pictures.</span></p></div>
    <div>{tick()}<p style="margin:0"><b>PDF tools</b><span>Merge, split, compress, page ranges, and PDF → PowerPoint slides.</span></p></div>
    <div>{tick()}<p style="margin:0"><b>Video and audio</b><span>Smaller MP4, 720p, GIF from video, every frame as pictures, audio extraction.</span></p></div>
    <div>{tick()}<p style="margin:0"><b>Archives</b><span>Extract ZIP, RAR, 7Z, ISO, or put any files into one ZIP.</span></p></div>
  </div>
</div></section>

<section class="sec"><div class="wrap">
  <div class="sec-head"><div class="kicker">Compared</div><h2>DropMorph vs online converters</h2></div>
  <div class="vs">
    <div class="card"><h3>Online converter sites</h3><ul>
      <li><span class="bad">✕</span><span>Your file is <b>uploaded</b> to an unknown server</span></li>
      <li><span class="bad">✕</span><span>Ads, waiting rooms and "upgrade to Pro"</span></li>
      <li><span class="bad">✕</span><span>Size limits and daily limits</span></li>
      <li><span class="bad">✕</span><span>One file at a time, needs internet</span></li></ul></div>
    <div class="card"><h3>DropMorph</h3><ul>
      <li><span class="good">✓</span><span>Converted <b>on your PC</b>, nothing leaves it</span></li>
      <li><span class="good">✓</span><span>No ads, no account, <b>free forever</b></span></li>
      <li><span class="good">✓</span><span><b>No limits</b> on size or number of files</span></li>
      <li><span class="good">✓</span><span>Batch conversion, <b>works offline</b></span></li></ul></div>
  </div>
</div></section>

<section class="sec alt" id="formats"><div class="wrap">
  <div class="sec-head"><div class="kicker">Formats</div><h2>{FORMAT_COUNT} formats in 18 groups</h2><p>A quick look at what goes in. Marked ones use an optional engine.</p></div>
  <div class="formats">{teaser}</div>
  <p style="margin-top:24px"><a class="btn btn-line" href="/formats/">See every format and what it converts to →</a></p>
</div></section>

<section class="sec" id="cli"><div class="wrap grid2" style="align-items:center">
  <div class="sec-head" style="margin:0"><div class="kicker">Command line &amp; watch folders</div><h2>Prefer typing? Or no typing at all?</h2>
    <p>The <code>dropmorph</code> command uses the same engines and presets as the app. Point a watch folder at your Downloads or a scanner's output and every new file is converted on its own.</p>
    <p style="margin-top:18px"><a class="btn btn-line" href="/cli/">Command line reference →</a></p></div>
<pre class="term"><span class="c"># one file</span>
<span class="p">&gt;</span> dropmorph convert photo.heic --to jpg

<span class="c"># a whole folder, smaller files</span>
<span class="p">&gt;</span> dropmorph convert *.png --to webp --quality 80 -o converted

<span class="c"># scans folder → PDFs, sub folders too</span>
<span class="p">&gt;</span> dropmorph convert D:\\Scans --recursive --preset to-pdf

<span class="c"># convert everything that lands in a folder</span>
<span class="p">&gt;</span> dropmorph watch D:\\Inbox --to pdf --move-originals</pre>
</div></section>

<section class="sec alt"><div class="wrap">
  <div class="sec-head"><div class="kicker">Make it yours</div><h2>Your donut, your colours, your language</h2></div>
  <div class="grid3">
    <div class="card"><div class="icon">{icon("sliders")}</div><h3>Arrange the donut</h3><p>One toolbar per file type. Drag formats and presets into the order you use most, and save your own presets.</p></div>
    <div class="card"><div class="icon coral">{icon("palette")}</div><h3>Light, dark and custom themes</h3><p>Pick any accent colour, or replace every colour with a small theme file.</p></div>
    <div class="card"><div class="icon">{icon("globe")}</div><h3>Six languages</h3><p>English, हिन्दी, Español, 한국어, 简体中文 and Polski, switchable in Settings.</p></div>
  </div>
</div></section>

<section class="sec" id="faq"><div class="wrap narrow">
  <div class="sec-head"><div class="kicker">FAQ</div><h2>Questions &amp; answers</h2></div>
  {faq}
</div></section>

<section class="sec alt" id="glance"><div class="wrap narrow">
  <div class="sec-head"><div class="kicker">At a glance</div><h2>DropMorph in one table</h2></div>
  <div class="tbl-wrap"><table class="tbl"><tbody>{glance_rows}</tbody></table></div>
</div></section>

<section class="sec" id="download"><div class="wrap">
  <div class="sec-head"><div class="kicker">Download</div><h2>Get DropMorph. It's free.</h2><p>Windows, macOS and Linux, with Android and iPhone help below. Pick your computer. Download the file, or install with one command. Settings and engines are kept when DropMorph updates itself.</p></div>
  <div class="tip phone-note" role="note"><p><b>On a phone?</b> DropMorph runs on a computer (Windows, Mac or Linux), not on Android or iPhone. The files below still download, but to install it, open this page on your computer.</p>
    <button class="btn btn-accent btn-small" type="button" data-share>Share link</button></div>
  <div class="os-grid">
    <article class="card os-card" data-os="windows" aria-labelledby="os-win">
      <div class="os-head"><h3 id="os-win">{os_logo("windows")}Windows</h3><span class="os-you">Your computer</span></div>
      <p class="os-req">Windows 10 (1809+) and 11, 64-bit</p>
      <p>Setup adds the right-click menu, start at sign-in and the <code>dropmorph</code> command. The portable ZIP runs without installing.</p>
      <div class="os-btns"><a class="btn btn-accent" href="{SETUP_URL}">{icon("download")} Setup (112 MB)</a><a class="btn btn-line" href="{PORTABLE_URL}">{icon("download")} Portable ZIP</a></div>
      {cmd_box("Or one command in PowerShell", INSTALL_PS1)}
      <a class="os-guide" href="/install/">Windows install guide →</a>
    </article>
    <article class="card os-card" data-os="mac" aria-labelledby="os-mac">
      <div class="os-head"><h3 id="os-mac">{os_logo("mac")}macOS</h3><span class="os-you">Your computer</span></div>
      <p class="os-req">macOS 14 Sonoma or newer</p>
      <p>The one command below is the easiest: it picks the right file for your Mac and installs it, without the "Open Anyway" step.</p>
      <div class="os-btns"><a class="btn btn-accent" href="{MAC_ARM_URL}" data-arch="arm">{icon("download")} Apple silicon (M1–M4)<span class="rec-tag">Your Mac</span></a><a class="btn btn-accent" href="{MAC_INTEL_URL}" data-arch="intel">{icon("download")} Intel Mac<span class="rec-tag">Your Mac</span></a></div>
      <p class="os-hint">Which one? Apple menu → About This Mac → <b>Chip</b> (Apple M1–M4) or <b>Processor</b> (Intel).</p>
      {cmd_box("Or one command in Terminal", INSTALL_SH)}
      <a class="os-guide" href="/install/mac/">Mac install guide →</a>
    </article>
    <article class="card os-card" data-os="linux" aria-labelledby="os-linux">
      <div class="os-head"><h3 id="os-linux">{os_logo("linux")}Linux</h3><span class="os-you">Your computer</span></div>
      <p class="os-req">Ubuntu 22.04+ and similar</p>
      <p>Unpack and run <code>./install.sh</code>, no root needed. Drag gestures need an X11 session.</p>
      <div class="os-btns"><a class="btn btn-accent" href="{LINUX_X64_URL}">{icon("download")} x64 (most PCs)</a><a class="btn btn-line" href="{LINUX_ARM_URL}">{icon("download")} ARM64</a></div>
      {cmd_box("Or one command in a terminal", INSTALL_SH)}
      <a class="os-guide" href="/install/linux/">Linux install guide →</a>
    </article>
  </div>
  <div class="sec-head phone-head"><h3>On a phone? Android &amp; iPhone</h3><p>DropMorph is a computer app, so there is no phone version yet. Send the link to your computer and install it there in a minute.</p></div>
  <div class="os-grid phone-grid">
    <article class="card os-card" data-os="android" aria-labelledby="os-android">
      <div class="os-head"><h3 id="os-android">{os_logo("android")}Android</h3><span class="os-soon">Coming soon</span><span class="os-you">Your phone</span></div>
      <p>Open this page on your Windows, Mac or Linux computer to install DropMorph. Share the link to yourself (WhatsApp, email, Telegram).</p>
      <div class="os-btns"><button class="btn btn-accent" type="button" data-share>Send link to my computer</button><a class="btn btn-line" href="{GH}">{gh_icon()} ⭐ Star to vote for a phone app</a></div>
    </article>
    <article class="card os-card" data-os="ios" aria-labelledby="os-ios">
      <div class="os-head"><h3 id="os-ios">{os_logo("mac")}iPhone &amp; iPad</h3><span class="os-soon">Coming soon</span><span class="os-you">Your phone</span></div>
      <p>Open this page on your Mac or PC to install DropMorph. Tip: on an iPhone, AirDrop the link to your Mac.</p>
      <div class="os-btns"><button class="btn btn-accent" type="button" data-share>Send link to my computer</button><a class="btn btn-line" href="{GH}">{gh_icon()} ⭐ Star to vote for a phone app</a></div>
    </article>
  </div>
  <p class="note">Releases aren't code-signed yet. On Windows, SmartScreen may warn: click "More info" → "Run anyway". On a Mac, the one command avoids the warning; the Mac guide shows the manual way. Every release is built by GitHub Actions from the public source code, with SHA-256 checksums. <a href="{GH}/releases">All releases</a></p>
  {star_card()}
</div></section>
{final_cta()}
"""
    ld = [{"@context": "https://schema.org", "@type": "SoftwareApplication", "name": "DropMorph",
           "description": f"Free, offline drag-and-drop file converter for {FORMAT_COUNT} formats.",
           "applicationCategory": "UtilitiesApplication", "operatingSystem": "Windows 10, Windows 11, macOS 14+, Linux",
           "softwareVersion": VERSION, "downloadUrl": SETUP_URL, "url": BASE + "/", "license": GH + "/blob/main/LICENSE",
           "offers": {"@type": "Offer", "price": "0", "priceCurrency": "USD"},
           "author": {"@type": "Person", "name": "Nitish Singh", "url": "https://github.com/cod4nitish"}},
          faq_ld(HOME_FAQ)]
    return page("/", "DropMorph: Free File Converter for Windows, Offline | Drag. Drop. Morphed.",
                f"Convert {FORMAT_COUNT} file formats by dragging: PDF to Word, Excel to PDF, HEIC to JPG, MP4 to MP3 and more. Free, offline, nothing uploaded. Windows, macOS and Linux.",
                body, jsonld=ld)


# ─────────────────────────── formats page ───────────────────────────

def formats_page():
    rows = read_formats()
    cards = "".join(f'<div class="fmt" id="{re.sub(r"[^a-z0-9]+", "-", n.lower()).strip("-")}"><h3>{esc(n)}</h3>'
                    f'<span class="lbl">From</span><p class="val">{fmt_html(s)}</p>'
                    f'<span class="lbl">To</span><p>{fmt_html(d)}</p></div>' for n, s, d in rows)
    toc = "".join(f'<a href="#{re.sub(r"[^a-z0-9]+", "-", n.lower()).strip("-")}">{esc(n)}</a>' for n, _, _ in rows)
    eng = "".join(f"<tr><td>{esc(n)}</td><td>{esc(s)}</td><td>{esc(w)}</td></tr>" for n, s, w in ENGINE_INFO)
    pills = "".join(f'<a class="pill-link" href="/convert/{c["slug"]}/">{esc(c["from"])} <i>→</i> {esc(c["to"])}</a>' for c in CONVERSIONS)
    body = f"""
<header class="hero"><div class="wrap hero-simple">
  <div class="crumbs"><a href="/">DropMorph</a> › Formats</div>
  <h1>{FORMAT_COUNT} formats in <span>18 groups</span></h1>
  <p class="lead">Everything DropMorph reads and writes. Unmarked formats work out of the box, without Office or LibreOffice. Marked ones use a free engine DropMorph downloads the first time you need it.</p>
  <div class="cta">{dl_btn()}</div>
</div></header>
<section class="sec"><div class="wrap">
  <div class="sec-head"><div class="kicker">Popular conversions</div><h2>Step-by-step guides</h2></div>
  <div class="pills">{pills}</div>
</div></section>
<section class="sec alt"><div class="wrap">
  <div class="sec-head"><div class="kicker">All formats</div><h2>What goes in, what comes out</h2></div>
  <div class="toc" style="margin-bottom:22px">{toc}</div>
  <div class="formats">{cards}</div>
</div></section>
<section class="sec"><div class="wrap narrow">
  <div class="sec-head"><div class="kicker">Optional engines</div><h2>Downloaded only when needed</h2><p>Each engine is a free program pinned to an official download and checked (SHA-256) before use. Manage them any time in Settings → Engines.</p></div>
  <div class="tbl-wrap"><table class="tbl"><thead><tr><th>Engine</th><th>Download</th><th>Used for</th></tr></thead><tbody>{eng}</tbody></table></div>
  <p class="note">The 7-Zip engine for archives comes with the installer.</p>
</div></section>
{final_cta()}
"""
    return page("/formats/", f"All {FORMAT_COUNT} Supported Formats | DropMorph File Converter",
                "Every format DropMorph converts: images, PDF, Word, Excel, PowerPoint, e-books, video, audio, archives, CAD, fonts, data, 3D and map files. Free and offline.",
                body, active="Formats")


# ─────────────────────────── CLI page ───────────────────────────

CONVERT_OPTS = [("-t, --to <format>", "Target format, e.g. jpg, png, pdf, docx, mp4 (see <code>dropmorph formats</code>)"),
                ("-p, --preset <preset>", "A preset from the app, by id or name (see <code>dropmorph presets</code>)"),
                ("-o, --output <folder>", "Save there instead of next to the sources"),
                ("-r, --recursive", "Include sub folders"),
                ("-q, --quality <1-100>", "Image quality for JPG, WebP, AVIF, …"),
                ("--overwrite", "Replace existing output files (default: add a number)"),
                ("--skip", "Leave existing output files alone"),
                ("--keep-dates", "Give converted files the created and modified dates of the originals"),
                ("--zip", "Save the result of each file as one ZIP"),
                ("--json", "Print the result as JSON, for scripts and other programs"),
                ("--quiet", "Print nothing but errors")]
WATCH_OPTS = [("-t, --to <format>", "Target format"), ("-p, --preset <preset>", "A preset from the app, by id or name"),
              ("-o, --output <folder>", "Where converted files go (default: <code>&lt;folder&gt;\\converted</code>)"),
              ("-r, --recursive", "Also watch sub folders; their files keep the sub folder in the output"),
              ("--move-originals", "Move converted sources to <code>&lt;folder&gt;\\originals</code>; files already waiting are converted at start"),
              ("--settle <seconds>", "How long a file must stay unchanged before converting (default 2)"),
              ("--keep-dates", "Keep the originals' created and modified dates"), ("--zip", "Save each file's result as one ZIP"),
              ("--quiet", "Print nothing but errors")]


def cli_page():
    def table(rows):
        return "<div class='tbl-wrap'><table class='tbl'><thead><tr><th>Option</th><th>What it does</th></tr></thead><tbody>" + \
            "".join(f"<tr><td><code>{esc(o)}</code></td><td>{d}</td></tr>" for o, d in rows) + "</tbody></table></div>"
    body = f"""
<header class="hero"><div class="wrap hero-simple">
  <div class="crumbs"><a href="/">DropMorph</a> › Command line</div>
  <h1>The <span>dropmorph</span> command</h1>
  <p class="lead">Convert files and folders from a terminal, a script or a scheduled task, with the same engines and presets as the app. Or let a watch folder do it on its own.</p>
</div></header>
<section class="sec"><div class="wrap narrow prose">
  <div class="toc"><a href="#setup">Set up</a><a href="#usage">Usage</a><a href="#convert">convert</a><a href="#watch">watch</a><a href="#more">formats &amp; presets</a><a href="#exit">Exit codes</a></div>

  <h2 id="setup">Set up</h2>
  <p><b>Windows:</b> in Setup, tick <b>Add the "dropmorph" command to PATH</b>. Open a new terminal (PowerShell, Command Prompt or Windows Terminal) and run <code>dropmorph help</code>.</p>
  <p><b>macOS and Linux:</b> the one-line installer (<code>{INSTALL_SH}</code>) or the package's <code>./install.sh</code> adds the command.</p>

  <h2 id="usage">Usage</h2>
<pre class="term">dropmorph convert &lt;files or folders...&gt; --to &lt;format&gt; [options]
dropmorph convert &lt;files or folders...&gt; --preset &lt;preset&gt; [options]
dropmorph watch &lt;folder&gt; --to &lt;format&gt; [options]
dropmorph formats [&lt;format&gt;]
dropmorph presets
dropmorph help [&lt;command&gt;]
dropmorph version</pre>

  <h2 id="convert">convert</h2>
  <p>Files can be wildcards (<code>*.heic</code>). Folders convert the files in them that DropMorph knows. Results are saved next to the sources unless you give <code>-o</code>.</p>
<pre class="term"><span class="c"># a photo</span>
<span class="p">&gt;</span> dropmorph convert photo.heic --to jpg
<span class="c"># every PNG, smaller WebP files in a "converted" folder</span>
<span class="p">&gt;</span> dropmorph convert *.png --to webp --quality 80 -o converted
<span class="c"># a scans folder and its sub folders, with an app preset</span>
<span class="p">&gt;</span> dropmorph convert D:\\Scans --recursive --preset to-pdf
<span class="c"># machine-readable result</span>
<span class="p">&gt;</span> dropmorph convert report.docx --to pdf --json</pre>
  {table(CONVERT_OPTS)}

  <h2 id="watch">watch</h2>
  <p>Converts files that land in a folder until you press <kbd>Ctrl</kbd>+<kbd>C</kbd>. A file is converted once it's complete: no longer growing or open in another program. Output goes to a <code>converted</code> folder inside it unless <code>-o</code> is given.</p>
<pre class="term"><span class="c"># every new file in D:\\Inbox becomes a PDF; originals are moved aside</span>
<span class="p">&gt;</span> dropmorph watch D:\\Inbox --to pdf --move-originals</pre>
  {table(WATCH_OPTS)}
  <div class="tip"><b>Tip:</b> watch folders are also in the app (Settings → Watch folders), so they keep running in the tray without a terminal.</div>

  <h2 id="more">formats and presets</h2>
  <p><code>dropmorph formats</code> lists every format, and <code>dropmorph formats heic</code> shows what one format converts to. <code>dropmorph presets</code> lists the presets saved in the app, both built-in and your own; use them with <code>--preset</code>.</p>

  <h2 id="exit">Exit codes</h2>
  <div class="tbl-wrap"><table class="tbl"><tbody>
    <tr><td><code>0</code></td><td>All files converted</td></tr>
    <tr><td><code>1</code></td><td>Some files failed</td></tr>
    <tr><td><code>2</code></td><td>Wrong arguments or a missing input</td></tr>
    <tr><td><code>3</code></td><td>Nothing to convert</td></tr></tbody></table></div>
</div></section>
{final_cta()}
"""
    return page("/cli/", "Command Line File Converter: dropmorph convert & watch | DropMorph",
                "Convert files from the command line with dropmorph convert, or automatically with dropmorph watch folders. Options, examples and exit codes.",
                body, active="Command line")


# ─────────────────────────── main ───────────────────────────

def main():
    write("index.html", home())
    write("formats/index.html", formats_page())
    write("cli/index.html", cli_page())
    for c in CONVERSIONS:
        write(f"convert/{c['slug']}/index.html", conv_page(c))
    urls = ["/", "/install/", "/install/mac/", "/install/linux/", "/formats/", "/cli/"] + [f"/convert/{c['slug']}/" for c in CONVERSIONS]
    write("sitemap.xml", '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' +
          "".join(f"  <url><loc>{BASE}{u}</loc><lastmod>{RELEASED}</lastmod></url>\n" for u in urls) + "</urlset>\n")
    write("robots.txt", f"User-agent: *\nAllow: /\n\nSitemap: {BASE}/sitemap.xml\n")
    print(f"built {len(urls)} pages")


if __name__ == "__main__":
    main()
