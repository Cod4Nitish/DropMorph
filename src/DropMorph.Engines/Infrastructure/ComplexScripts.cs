// Scripts whose text needs complex shaping to be drawn: letters that join (Arabic), right-to-left order (Arabic,
// Hebrew), vowel signs drawn before the consonant they follow, conjuncts and stacked marks (the Indic scripts, Thai,
// Khmer, Myanmar, Tibetan, ...). rhwp draws one glyph per character from left to right without any of that, so a
// document with such text goes to LibreOffice instead (RhwpConverter, issues #185 and #202); the PDF reader puts the
// characters of such text back in their logical order (PdfDocumentReader.ComplexText).
//
// FindInFile scans a document's text cheaply, without reading it into the document model: the content parts of an
// Office Open XML / OWPML / OpenDocument / EPUB package as they are (markup is ASCII, so any match is text), a text
// file decoded like the plain text reader does (character references of HTML and RTF included).

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DropMorph.Engines.Hwp.Hwpx;

namespace DropMorph.Engines.Infrastructure;

/// <summary>Finds text in scripts that need complex shaping.</summary>
internal static partial class ComplexScripts
{
    /// <summary>Unicode blocks of scripts that need shaping, by first code point, with the script's English name.</summary>
    private static readonly (int Start, int End, string Name)[] Blocks =
    [
        (0x0590, 0x05FF, "Hebrew"),
        (0x0600, 0x06FF, "Arabic"),
        (0x0700, 0x074F, "Syriac"),
        (0x0750, 0x077F, "Arabic"),
        (0x0780, 0x07BF, "Thaana"),
        (0x07C0, 0x07FF, "N'Ko"),
        (0x0800, 0x083F, "Samaritan"),
        (0x0840, 0x085F, "Mandaic"),
        (0x0860, 0x086F, "Syriac"),
        (0x0870, 0x08FF, "Arabic"),
        (0x0900, 0x097F, "Devanagari"),
        (0x0980, 0x09FF, "Bengali"),
        (0x0A00, 0x0A7F, "Gurmukhi"),
        (0x0A80, 0x0AFF, "Gujarati"),
        (0x0B00, 0x0B7F, "Odia"),
        (0x0B80, 0x0BFF, "Tamil"),
        (0x0C00, 0x0C7F, "Telugu"),
        (0x0C80, 0x0CFF, "Kannada"),
        (0x0D00, 0x0D7F, "Malayalam"),
        (0x0D80, 0x0DFF, "Sinhala"),
        (0x0E00, 0x0E7F, "Thai"),
        (0x0E80, 0x0EFF, "Lao"),
        (0x0F00, 0x0FFF, "Tibetan"),
        (0x1000, 0x109F, "Myanmar"),
        (0x1780, 0x17FF, "Khmer"),
        (0x1800, 0x18AF, "Mongolian"),
        (0x1900, 0x194F, "Limbu"),
        (0x1950, 0x19DF, "Tai Le"),
        (0x19E0, 0x19FF, "Khmer"),
        (0x1A00, 0x1A1F, "Buginese"),
        (0x1A20, 0x1AAF, "Tai Tham"),
        (0x1B00, 0x1B7F, "Balinese"),
        (0x1B80, 0x1BBF, "Sundanese"),
        (0x1BC0, 0x1BFF, "Batak"),
        (0x1C00, 0x1C4F, "Lepcha"),
        (0xA800, 0xA82F, "Syloti Nagri"),
        (0xA880, 0xA8DF, "Saurashtra"),
        (0xA8E0, 0xA8FF, "Devanagari"),
        (0xA900, 0xA95F, "Kayah Li"),
        (0xA980, 0xA9DF, "Javanese"),
        (0xA9E0, 0xA9FF, "Myanmar"),
        (0xAA00, 0xAA5F, "Cham"),
        (0xAA60, 0xAA7F, "Myanmar"),
        (0xAA80, 0xAADF, "Tai Viet"),
        (0xAAE0, 0xAAFF, "Meetei Mayek"),
        (0xABC0, 0xABFF, "Meetei Mayek"),
        (0xFB1D, 0xFB4F, "Hebrew"),
        (0xFB50, 0xFDFF, "Arabic"),
        (0xFE70, 0xFEFF, "Arabic"),
        (0x10800, 0x10FFF, "right-to-left"),
        (0x11000, 0x11FFF, "Brahmic"),
    ];

    /// <summary>The script of <paramref name="codePoint"/> when it needs shaping ("Arabic", "Gujarati"), else null.</summary>
    public static string? ScriptOf(int codePoint)
    {
        // Latin, Greek, Cyrillic, Armenian; CJK, Hangul and punctuation in between: the common case, answered at once.
        if (codePoint < 0x0590 || codePoint is >= 0x2000 and < 0xA800 or >= 0xAC00 and < 0xFB1D)
            return null;
        foreach (var (start, end, name) in Blocks)
        {
            if (codePoint < start)
                return null;
            if (codePoint <= end)
                return name;
        }
        return null;
    }

    public static bool IsComplex(int codePoint) => ScriptOf(codePoint) is not null;

    /// <summary>A letter of a right-to-left script (Hebrew, Arabic, Syriac, Thaana, N'Ko, ...).</summary>
    public static bool IsRightToLeft(int codePoint) =>
        codePoint is >= 0x0590 and <= 0x08FF or >= 0xFB1D and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF
            or >= 0x10800 and <= 0x10FFF or >= 0x1E800 and <= 0x1EFFF;

    /// <summary>
    /// The direction of <paramref name="text"/> by its letters: -1 when it has right-to-left letters and at least as many
    /// of them as left-to-right ones, 1 when it has left-to-right letters or digits only, 0 without either.
    /// </summary>
    public static int Direction(string text)
    {
        int rtl = 0, ltr = 0, digits = 0;
        for (var i = 0; i < text.Length; i++)
        {
            int codePoint = text[i];
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                codePoint = char.ConvertToUtf32(text[i], text[++i]);
            if (ComplexScripts.IsRightToLeft(codePoint))
            {
                // Arabic-Indic digits are numbers: they keep their left-to-right order within the right-to-left text.
                if (codePoint is >= 0x0660 and <= 0x0669 or >= 0x06F0 and <= 0x06F9)
                    digits++;
                else if (Rune.IsLetter(new Rune(codePoint)))
                    rtl++;
            }
            else if (Rune.IsLetter(new Rune(codePoint)))
            {
                ltr++;
            }
            else if (Rune.IsDigit(new Rune(codePoint)))
            {
                digits++;
            }
        }
        return rtl > 0 && rtl >= ltr ? -1 : ltr + digits > 0 ? 1 : 0;
    }

    /// <summary>The first script in <paramref name="text"/> that needs shaping, or null.</summary>
    public static string? Find(ReadOnlySpan<char> text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c < 0x0590)
                continue;
            var codePoint = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? char.ConvertToUtf32(c, text[++i])
                : c;
            if (ScriptOf(codePoint) is { } script)
                return script;
        }
        return null;
    }

    /// <summary>True if <paramref name="text"/> has a character of a script that needs shaping.</summary>
    public static bool Contains(string text) => Find(text) is not null;

    /// <summary>
    /// The first script that needs shaping in the text of a document, or null (also for files that can't be read
    /// here: the conversion then reports what is wrong with them).
    /// </summary>
    /// <param name="format">Format id of the file (docx, txt, hwpx, ...).</param>
    public static string? FindInFile(string path, string format)
    {
        try
        {
            if (IsZip(path))
                return FindInPackage(path, format);
            // Web archives and mail are MIME: their HTML (or text) part is what reaches the PDF.
            if (format is "mhtml" or "eml")
            {
                var message = MimeKit.MimeMessage.Load(path);
                return FindInString(message.HtmlBody ?? message.TextBody ?? "");
            }
            return format is "txt" or "md" or "html" or "csv" or "tsv" or "fb2" or "rtf" or "rst" or "tex" or "org" or "adoc"
                or "typ" or "mediawiki" or "dbk" or "textile" or "man" or "opml"
                ? FindInText(path)
                : null; // binary formats (HWP, DOC, MOBI, ...): left to the engines
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            return null;
        }
    }

    private static bool IsZip(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[4];
        return stream.ReadAtLeast(head, 4, throwOnEndOfStream: false) == 4 && head is [0x50, 0x4B, 0x03, 0x04];
    }

    /// <summary>Text files are read whole (as the readers do), up to this size; a larger one is scanned in its first part.</summary>
    private const int MaxTextBytes = 64 * 1024 * 1024;

    private static string? FindInText(string path)
    {
        byte[] bytes;
        using (var stream = File.OpenRead(path))
        {
            bytes = new byte[(int)Math.Min(stream.Length, MaxTextBytes)];
            stream.ReadExactly(bytes);
        }
        return FindInString(DocumentReaders.DecodeText(bytes));
    }

    private static string? FindInString(string text)
    {
        if (Find(text) is { } script)
            return script;
        // &#2693; / &#x0A85; in HTML and XML, ⚓? in RTF.
        foreach (Match match in CharacterReference().Matches(text))
        {
            var value = match.Groups["hex"].Success
                ? int.Parse(match.Groups["hex"].ValueSpan, System.Globalization.NumberStyles.HexNumber, null)
                : int.Parse(match.Groups["dec"].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
            if (value < 0)
                value += 0x10000; // RTF writes code points above 32767 as negative numbers
            if (ScriptOf(value) is { } referenced)
                return referenced;
        }
        return null;
    }

    [GeneratedRegex(@"&#(?:[xX](?<hex>[0-9a-fA-F]{1,6})|(?<dec>[0-9]{1,7}));|\\u(?<dec>-?[0-9]{1,5})", RegexOptions.CultureInvariant)]
    private static partial Regex CharacterReference();

    /// <summary>The parts of a package that hold its text (not styles, themes or font tables, which name fonts per script).</summary>
    private static bool IsContentPart(string name, string format)
    {
        bool Xml(string folder, string prefix) =>
            name.StartsWith(folder + prefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            && name.IndexOf('/', folder.Length) < 0;
        return format switch
        {
            "docx" or "docm" or "dotx" or "dotm" => Xml("word/", "document") || Xml("word/", "header") || Xml("word/", "footer")
                                                     || Xml("word/", "footnotes") || Xml("word/", "endnotes"),
            "pptx" or "pptm" or "potx" or "ppsx" => Xml("ppt/slides/", "slide"),
            "xlsx" or "xlsm" or "xltx" => Xml("xl/", "sharedStrings") || Xml("xl/worksheets/", "sheet"),
            "hwpx" => Xml("Contents/", "section"),
            "odt" or "ods" or "odp" => name.Equals("content.xml", StringComparison.OrdinalIgnoreCase),
            // OFD: the pages and templates (Doc_0/Pages/Page_0/Content.xml, Doc_0/Tpls/Tpl_0/Content.xml).
            "ofd" => name.EndsWith("/Content.xml", StringComparison.OrdinalIgnoreCase),
            // EPUB, HTMLZ, TXTZ, ...: the chapters.
            _ => !name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)
                 && (name.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                     || name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)),
        };
    }

    private static string? FindInPackage(string path, string format)
    {
        using var zip = ZipFile.OpenRead(path);
        var buffer = new char[64 * 1024];
        foreach (var entry in zip.Entries)
        {
            if (!IsContentPart(entry.FullName, format))
                continue;
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                // A surrogate pair cut at the end of the buffer only hides a supplementary-plane letter there.
                if (Find(buffer.AsSpan(0, read)) is { } script)
                    return script;
            }
        }
        return null;
    }
}
