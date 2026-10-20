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
    <a class="brand" href="/"><img src="/logo.svg" alt="" width="32" height="32">DropMorph</a>
    <div class="navlinks">{nav}</div>
    <button class="theme-toggle" id="themeToggle" type="button" aria-label="Switch light or dark theme">◐</button>
    <a class="btn btn-primary btn-small nav-dl" href="/#download">Download</a>
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
      <div><h4>Product</h4><a href="/#features">Features</a><a href="/formats/">All formats</a><a href="/cli/">Command line</a><a href="/install/">Windows install guide</a><a href="/#faq">FAQ</a></div>
      <div><h4>Popular</h4>{popular}</div>
      <div><h4>Project</h4><a href="{GH}">GitHub</a><a href="{GH}/releases">All releases</a><a href="{GH}/issues">Report an issue</a><a href="{GH}/blob/main/LICENSE">MIT License</a><a href="{GH}/blob/main/THIRD-PARTY-NOTICES.md">Third-party notices</a></div>
    </div>
    <div class="foot-bottom"><span>© 2026 DropMorph · Made by <a href="https://github.com/cod4nitish">Nitish Singh</a></span><span>Version {VERSION}</span></div>
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
  <div class="cta"><a class="btn btn-primary" href="{SETUP_URL}">{icon("download")} Download for Windows</a><a class="btn btn-ghost" href="/install/">Install guide</a>
  <small>Free · Offline · Windows 10 &amp; 11 · macOS and Linux preview</small></div>
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
  <div class="cta" style="justify-content:center"><a class="btn btn-primary" href="{SETUP_URL}">{icon("download")} Download for Windows</a><a class="btn btn-ghost" href="/install/">Windows 11 install guide</a></div>
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
    ("Does installing need administrator rights?", "The Windows Setup asks for administrator permission once. The portable ZIP runs without installing."),
    ("Can I convert many files at once?", "Yes. Select as many files as you like and drop them together; they convert in parallel. Watch folders convert every file that lands in them."),
    ("Can I convert from the Windows 11 right-click menu?", "Yes. Setup can add DropMorph to the right-click menu, including the top of the compact Windows 11 menu."),
    ("Can it convert from the command line or automatically?", "Yes. The dropmorph command converts files and folders, and dropmorph watch converts everything that lands in a folder. See the Command line page."),
    ("How do I convert a file, for example HEIC to JPG?", "Hold Ctrl, drag the HEIC photo and drop it on JPG in the donut. The JPG is saved next to the original."),
    ("Why use DropMorph instead of an online converter?", "Online converters upload your files to someone else's server, show ads and limit file sizes. DropMorph is faster, private, has no limits and works without internet."),
    ("Which operating systems are supported?", "Windows 10 (1809 or newer) and Windows 11 are fully supported. macOS 14+ and Linux (Ubuntu 22.04+) are available as previews."),
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
              ("macOS / Linux", "Preview: macOS 14+, Ubuntu 22.04+ (x64 and ARM64)"),
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
        <a class="btn btn-primary" href="{SETUP_URL}">{icon("download")} Download for Windows</a>
        <a class="btn btn-ghost" href="/install/">Install guide</a>
        <small>Windows 10 &amp; 11 · macOS and Linux preview · v{VERSION}</small>
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
  <div class="sec-head"><div class="kicker">Download</div><h2>Get DropMorph. It's free.</h2><p>Settings and engines are kept when DropMorph updates itself.</p></div>
  <div class="grid3">
    <div class="card"><h3 style="margin-top:0">Windows 10 &amp; 11</h3><p>Setup adds the right-click menu, start at sign-in and the <code>dropmorph</code> command. Or use the portable ZIP.</p>
      <div class="cta" style="margin-top:16px"><a class="btn btn-accent" href="{SETUP_URL}">Setup (112 MB)</a><a class="btn btn-line" href="{PORTABLE_URL}">Portable ZIP</a></div>
      <p style="margin-top:12px"><a href="/install/" style="color:var(--accent);font-weight:700">Windows 11 install guide →</a></p></div>
    <div class="card"><h3 style="margin-top:0">macOS <span class="eng">Preview</span></h3><p>macOS 14+, Apple silicon and Intel. Unpack and run <code>./install.sh</code>, then allow it in Privacy &amp; Security.</p>
      <div class="cta" style="margin-top:16px"><a class="btn btn-accent" href="{GH}/releases/latest">Download for macOS</a></div></div>
    <div class="card"><h3 style="margin-top:0">Linux <span class="eng">Preview</span></h3><p>Ubuntu 22.04+, x64 and ARM64. Unpack and run <code>./install.sh</code> (no root). Drag gestures need X11.</p>
      <div class="cta" style="margin-top:16px"><a class="btn btn-accent" href="{GH}/releases/latest">Download for Linux</a></div></div>
  </div>
  <p class="note">Windows releases aren't code-signed yet, so SmartScreen may warn: click "More info" → "Run anyway". Every release is built by GitHub Actions from the public source code, with SHA-256 checksums.</p>
</div></section>
{final_cta()}
"""
    ld = [{"@context": "https://schema.org", "@type": "SoftwareApplication", "name": "DropMorph",
           "description": f"Free, offline drag-and-drop file converter for {FORMAT_COUNT} formats.",
           "applicationCategory": "UtilitiesApplication", "operatingSystem": "Windows 10, Windows 11, macOS, Linux",
           "softwareVersion": VERSION, "downloadUrl": SETUP_URL, "url": BASE + "/", "license": GH + "/blob/main/LICENSE",
           "offers": {"@type": "Offer", "price": "0", "priceCurrency": "USD"},
           "author": {"@type": "Person", "name": "Nitish Singh", "url": "https://github.com/cod4nitish"}},
          faq_ld(HOME_FAQ)]
    return page("/", "DropMorph: Free File Converter for Windows, Offline | Drag. Drop. Morphed.",
                f"Convert {FORMAT_COUNT} file formats by dragging: PDF to Word, Excel to PDF, HEIC to JPG, MP4 to MP3 and more. Free, offline, nothing uploaded. Windows 10 & 11.",
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
  <div class="cta"><a class="btn btn-primary" href="{SETUP_URL}">{icon("download")} Download for Windows</a></div>
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
  <p><b>macOS and Linux:</b> <code>./install.sh</code> adds the command.</p>

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
    urls = ["/", "/install/", "/formats/", "/cli/"] + [f"/convert/{c['slug']}/" for c in CONVERSIONS]
    write("sitemap.xml", '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' +
          "".join(f"  <url><loc>{BASE}{u}</loc><lastmod>{RELEASED}</lastmod></url>\n" for u in urls) + "</urlset>\n")
    write("robots.txt", f"User-agent: *\nAllow: /\n\nSitemap: {BASE}/sitemap.xml\n")
    print(f"built {len(urls)} pages")


if __name__ == "__main__":
    main()
