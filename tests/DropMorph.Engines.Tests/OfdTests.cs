// OFD (GB/T 33190, the Chinese fixed-layout standard: e-invoices, official documents) → DOCX / TXT with the
// built-in reader. The packages are written in the test: an e-invoice in the tax-invoice structure (template page
// with the frame and labels, values on the page, QR code and seal pictures), a WPS document (page size only in
// Document.xml, decimal colours, wrapped body text over two pages), and hostile packages.

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DropMorph.Core.Conversion;
using DropMorph.Core.Presets;
using DropMorph.Engines.Hwp.Hwpx;
using DropMorph.Engines.Ofd;
using ImageMagick;

namespace DropMorph.Engines.Tests;

public class OfdTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private const string Ns = "xmlns:ofd=\"http://www.ofdspec.org/2016\"";

    // ───────────────────────── E-invoice ─────────────────────────

    /// <summary>
    /// A fully digitalized e-invoice (数电票) as tax-invoice writers lay it out: the template draws the frame (a grid
    /// in the layer's red-brown) and the labels, some stacked one character per line (购买方信息) or spread (合 计);
    /// the page draws the values — numbers with DeltaX in the "g" shorthand, the buyer's name scaled by a CTM, an item
    /// name wrapped onto a second line — and a QR code. Signatures are present and ignored.
    /// </summary>
    private static string MakeInvoice(string folder)
    {
        var png = File.ReadAllBytes(EngineFixture.MakeImage(folder, "qr.png", MagickFormat.Png, 80, 80));
        var path = Path.Combine(folder, "invoice.ofd");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Text(zip, "OFD.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:OFD {Ns} Version="1.1" DocType="OFD"><ofd:DocBody>
            <ofd:DocInfo><ofd:DocID>4bd9a0e0</ofd:DocID><ofd:CustomDatas><ofd:CustomData Name="发票号码">25442000000123456789</ofd:CustomData></ofd:CustomDatas></ofd:DocInfo>
            <ofd:DocRoot>Doc_0/Document.xml</ofd:DocRoot><ofd:Signatures>Doc_0/Signs/Signatures.xml</ofd:Signatures>
            </ofd:DocBody></ofd:OFD>
            """);
        Text(zip, "Doc_0/Document.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Document {Ns}><ofd:CommonData><ofd:MaxUnitID>200</ofd:MaxUnitID>
            <ofd:PageArea><ofd:PhysicalBox>0 0 210 140</ofd:PhysicalBox></ofd:PageArea>
            <ofd:PublicRes>PublicRes.xml</ofd:PublicRes><ofd:DocumentRes>DocumentRes.xml</ofd:DocumentRes>
            <ofd:TemplatePage ID="2" BaseLoc="Tpls/Tpl_0/Content.xml"/></ofd:CommonData>
            <ofd:Pages><ofd:Page ID="1" BaseLoc="Pages/Page_0/Content.xml"/></ofd:Pages></ofd:Document>
            """);
        Text(zip, "Doc_0/PublicRes.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Res {Ns} BaseLoc="Res"><ofd:Fonts>
            <ofd:Font ID="3" FontName="宋体" FamilyName="宋体"/><ofd:Font ID="4" FontName="楷体" FamilyName="楷体"/>
            <ofd:Font ID="5" FontName="Courier New" FamilyName="Courier New"/></ofd:Fonts>
            <ofd:DrawParams><ofd:DrawParam ID="6" LineWidth="0.25"><ofd:FillColor Value="128 0 0"/><ofd:StrokeColor Value="128 0 0"/></ofd:DrawParam></ofd:DrawParams>
            </ofd:Res>
            """);
        Text(zip, "Doc_0/DocumentRes.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Res {Ns} BaseLoc="Res"><ofd:MultiMedias>
            <ofd:MultiMedia ID="7" Type="Image" Format="PNG"><ofd:MediaFile>image_7.png</ofd:MediaFile></ofd:MultiMedia>
            </ofd:MultiMedias></ofd:Res>
            """);
        Entry(zip, "Doc_0/Res/image_7.png", png);

        var template = new StringBuilder();
        void Label(string text, double x, double y, double size = 3)
        {
            template.Append($"""<ofd:TextObject ID="1" Boundary="{x} {y - size} 60 {size + 1}" Font="3" Size="{size}"><ofd:TextCode X="0" Y="{size}">{text}</ofd:TextCode></ofd:TextObject>""");
        }
        void Rule(double x0, double y0, double x1, double y1) =>
            template.Append($"""<ofd:PathObject ID="1" Boundary="{x0} {y0} {Math.Max(1, x1 - x0)} {Math.Max(1, y1 - y0)}"><ofd:AbbreviatedData>M 0 0 L {x1 - x0} {y1 - y0}</ofd:AbbreviatedData></ofd:PathObject>""");
        Label("电子发票（普通发票）", 75, 12, 6);
        Label("发票号码：", 140, 18);
        Label("开票日期：", 140, 24);
        // The frame: an outer rectangle and the rules of the blocks (no rules between the item columns).
        template.Append("""<ofd:PathObject ID="1" Boundary="5 28 200 97"><ofd:AbbreviatedData>M 0 0 L 200 0 L 200 97 L 0 97 C</ofd:AbbreviatedData></ofd:PathObject>""");
        Rule(5, 50, 205, 50);
        Rule(5, 100, 205, 100);
        Rule(5, 108, 205, 108);
        Rule(12, 28, 12, 50);
        Rule(105, 28, 105, 50);
        Rule(112, 28, 112, 50);
        Rule(45, 100, 45, 108);
        Rule(12, 108, 12, 125);
        foreach (var (c, i) in "购买方信息".Select((c, i) => (c, i)))
            Label(c.ToString(), 7, 32 + 4 * i);
        foreach (var (c, i) in "销售方信息".Select((c, i) => (c, i)))
            Label(c.ToString(), 107, 32 + 4 * i);
        Label("名称：", 14, 36);
        Label("统一社会信用代码/纳税人识别号：", 14, 44);
        Label("名称：", 114, 36);
        Label("统一社会信用代码/纳税人识别号：", 114, 44);
        Label("项目名称", 15, 55);
        Label("规格型号", 50, 55);
        Label("单位", 75, 55);
        Label("数量", 95, 55);
        Label("单价", 115, 55);
        Label("金额", 140, 55);
        Label("税率/征收率", 160, 55);
        Label("税额", 185, 55);
        Label("合", 20, 96);
        Label("计", 35, 96);
        Label("价税合计（大写）", 7, 105);
        Label("（小写）", 140, 105);
        Label("备", 7, 114);
        Label("注", 7, 120);
        Label("开票人：", 8, 131);
        Text(zip, "Doc_0/Tpls/Tpl_0/Content.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Page {Ns}><ofd:Content><ofd:Layer ID="1" DrawParam="6">{template}</ofd:Layer></ofd:Content></ofd:Page>
            """);

        var page = new StringBuilder();
        void Value(string text, double x, double y, int font = 4, double size = 3.5, string extra = "") =>
            page.Append($"""<ofd:TextObject ID="1" Boundary="{x} {y - size} 80 {size + 1}" Font="{font}" Size="{size}"><ofd:FillColor Value="0 0 0"/><ofd:TextCode X="0" Y="{size}"{extra}>{text}</ofd:TextCode></ofd:TextObject>""");
        page.Append("""<ofd:ImageObject ID="1" ResourceID="7" Boundary="8 6 20 20" CTM="20 0 0 20 0 0"/>""");
        Value("25442000000123456789", 155.5, 18, 5, extra: " DeltaX=\"g 19 2.1\"");
        Value("2025年03月18日", 155.5, 24);
        // Size 7 halved by the CTM: 3.5 mm on the page, the baseline at y = 36.
        page.Append("""<ofd:TextObject ID="1" Boundary="24 32.5 60 5" Font="4" Size="7" CTM="0.5 0 0 0.5 0 0"><ofd:TextCode X="0" Y="7">成都双流科技有限公司</ofd:TextCode></ofd:TextObject>""");
        Value("91510100MA6CXXXX1A", 61, 44, 5);
        Value("深圳市南山区示例贸易有限公司", 124, 36);
        Value("91440300MA5FXXXX2B", 161, 44, 5);
        Value("*印刷品*转印组", 15, 61);
        Value("A4", 50, 61);
        Value("件", 76, 61);
        Value("2", 97, 61);
        Value("50.00", 115, 61);
        Value("100.00", 140, 61);
        Value("13%", 163, 61);
        Value("13.00", 185, 61);
        Value("件套装", 15, 65.5);
        Value("*信息技术服务*软件维护费", 15, 71);
        Value("次", 76, 71);
        Value("1", 97, 71);
        Value("200.00", 115, 71);
        Value("200.00", 140, 71);
        Value("6%", 163, 71);
        Value("12.00", 185, 71);
        Value("¥300.00", 140, 96);
        Value("¥25.00", 185, 96);
        Value("叁佰贰拾伍圆整", 50, 105);
        Value("¥325.00", 155, 105);
        Value("购方开户银行：中国银行成都分行", 14, 114);
        Value("张三", 20.5, 131);
        Text(zip, "Doc_0/Pages/Page_0/Content.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Page {Ns}><ofd:Template TemplateID="2" ZOrder="Background"/>
            <ofd:Content><ofd:Layer ID="1">{page}</ofd:Layer></ofd:Content></ofd:Page>
            """);
        Text(zip, "Doc_0/Signs/Signatures.xml", $"""<?xml version="1.0" encoding="UTF-8"?><ofd:Signatures {Ns}><ofd:Signature ID="1" BaseLoc="Sign_0/Signature.xml"/></ofd:Signatures>""");
        Entry(zip, "Doc_0/Signs/Sign_0/SignedValue.dat", [0x30, 0x82, 0x01, 0x00, 0xDE, 0xAD]);
        return path;
    }

    [Fact]
    public void Invoice_frame_becomes_a_table_with_the_labels_and_values_in_their_cells()
    {
        var folder = fx.NewFolder();
        var document = OfdReader.Read(MakeInvoice(folder), Path.Combine(folder, "media"));
        var section = document.Sections.Single();
        Assert.Equal(Hwp(210), section.Page!.Width);
        Assert.Equal(Hwp(140), section.Page.Height);

        var table = section.Blocks.OfType<HTable>().Single();
        Assert.Equal(4, table.Rows.Count);
        string CellText(HCell cell) => string.Join("\n", cell.Blocks.OfType<HParagraph>().Select(ParagraphText));
        var cells = table.Rows.SelectMany(r => r.Cells).Select(CellText).ToList();

        // Buyer and seller blocks side by side, each with its stacked block title; label and value read as one.
        Assert.Equal(["购买方信息", "名称：成都双流科技有限公司\n统一社会信用代码/纳税人识别号：91510100MA6CXXXX1A",
                      "销售方信息", "名称：深圳市南山区示例贸易有限公司\n统一社会信用代码/纳税人识别号：91440300MA5FXXXX2B"],
            table.Rows[0].Cells.Select(CellText));

        // The item list: one cell, a tabbed row per item (a wrapped name continues on a second line of its row's
        // paragraph), the spread 合 计 one word.
        var items = CellText(table.Rows[1].Cells.Single()).Split('\n');
        Assert.Equal("项目名称\t规格型号\t单位\t数量\t单价\t金额\t税率/征收率\t税额", items[0]);
        Assert.Equal("*印刷品*转印组\tA4\t件\t2\t50.00\t100.00\t13%\t13.00", items[1]);
        Assert.Equal("件套装", items[2]);
        Assert.Equal("*信息技术服务*软件维护费\t次\t1\t200.00\t200.00\t6%\t12.00", items[3]);
        Assert.Equal("合计\t¥300.00\t¥25.00", items[4]);
        Assert.Equal(4, table.Rows[1].Cells.Single().Blocks.Count);
        var itemRow = table.Rows[1].Cells.Single().Blocks.OfType<HParagraph>().ElementAt(1);
        Assert.Equal(Hwp(50 - 5.5), itemRow.Format.Tabs![0].Position); // the column where A4 starts, from the cell's text edge

        Assert.Contains("价税合计（大写）", cells);
        Assert.Contains("叁佰贰拾伍圆整\t（小写） ¥325.00", cells);
        Assert.Contains("备注", cells);
        Assert.Contains("购方开户银行：中国银行成都分行", cells);

        // The frame keeps the template's red-brown rules; labels keep its colour, values are black.
        Assert.Equal("#800000", table.Rows[0].Cells[0].Borders!.Value.Left.Color);
        var labels = table.Rows[0].Cells[1].Blocks.OfType<HParagraph>().First().Inlines.OfType<HText>().ToList();
        Assert.Equal("#800000", labels[0].Format.Color);
        Assert.Null(labels[1].Format.Color);
        Assert.Equal("KaiTi", labels[1].Format.EastAsianFont);
        Assert.Equal(1000, labels[1].Format.Size); // 3.5 mm: 7 mm halved by the CTM

        // Above the frame: the title centred, then number and date in reading order; the QR code floats beside them.
        var above = section.Blocks.TakeWhile(b => b is not HTable).OfType<HParagraph>().ToList();
        var lines = above.Select(ParagraphText).Where(t => t.Length > 0).ToList();
        Assert.Equal(["电子发票（普通发票）", "发票号码：25442000000123456789", "开票日期：2025年03月18日"], lines);
        Assert.Equal(HAlign.Center, above.Single(p => ParagraphText(p).StartsWith('电')).Format.Align);
        var qr = above[0].Inlines.OfType<HImage>().Single();
        Assert.Equal(new HAnchor("PAPER", "LEFT", Hwp(8), "PAPER", "TOP", Hwp(6), "IN_FRONT_OF_TEXT", true), qr.Anchor);
        Assert.Equal(Hwp(20), qr.Width);

        // Below the frame.
        Assert.Equal("开票人：张三", ParagraphText((HParagraph)section.Blocks[^1]));
    }

    [Fact]
    public async Task Invoice_becomes_docx_and_txt_in_reading_order()
    {
        var job = await fx.ConvertAsync([MakeInvoice(fx.NewFolder())], new Preset { TargetFormat = "docx" });
        Assert.Equal(JobState.Completed, job.State);
        using (var zip = ZipFile.OpenRead(job.Outputs.Single()))
        {
            var xml = XDocument.Load(zip.GetEntry("word/document.xml")!.Open());
            Assert.Single(xml.Descendants(), d => d.Name.LocalName == "tbl");
            Assert.Contains("SimSun", xml.ToString());
            Assert.Contains(zip.Entries, e => e.FullName.StartsWith("word/media/", StringComparison.Ordinal));

            // The text in document order is the invoice's reading order.
            var text = string.Concat(xml.Descendants().Where(d => d.Name.LocalName is "t" or "p").Select(d => d.Name.LocalName == "p" ? "\n" : d.Value));
            string[] order = ["电子发票（普通发票）", "发票号码：25442000000123456789", "购买方信息", "名称：成都双流科技有限公司", "销售方信息",
                "*印刷品*转印组", "件套装", "*信息技术服务*软件维护费", "合计", "价税合计（大写）", "¥325.00", "备注", "开票人：张三"];
            var positions = order.Select(s => text.IndexOf(s, StringComparison.Ordinal)).ToList();
            Assert.DoesNotContain(-1, positions);
            Assert.Equal(positions.Order(), positions);
        }

        // TXT takes a route that depends on the platform's engines (through HWPX and Unhwp where it runs).
        job = await fx.ConvertAsync([MakeInvoice(fx.NewFolder())], new Preset { TargetFormat = "txt" });
        Assert.Equal(JobState.Completed, job.State);
        var txt = await File.ReadAllTextAsync(job.Outputs.Single(), TestContext.Current.CancellationToken);
        Assert.Contains("发票号码：25442000000123456789", txt);
        Assert.Contains("开票人：张三", txt);
    }

    // ───────────────────────── WPS document ─────────────────────────

    /// <summary>
    /// A two-page document as WPS writes it: the page size only in Document.xml, BaseLoc naming the content file,
    /// a Res.xml next to Document.xml that nothing names, decimal colours, bold as Weight=700, one TextObject per line.
    /// </summary>
    private const string Body = "为进一步提升医院管理水平和医疗服务质量，按照国家卫生健康委员会关于推进智慧医院建设的有关要求，结合本院实际情况，制定本方案。明确建设目标。";

    private static string MakeWps(string folder)
    {
        var path = Path.Combine(folder, "letter.ofd");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Text(zip, "OFD.xml", $"""<?xml version="1.0" encoding="utf-8"?><ofd:OFD {Ns} Version="1.1"><ofd:DocBody><ofd:DocRoot>/Doc_0/Document.xml</ofd:DocRoot></ofd:DocBody></ofd:OFD>""");
        Text(zip, "Doc_0/Document.xml", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <ofd:Document {Ns}><ofd:CommonData><ofd:PageArea><ofd:PhysicalBox>0 0 210 297</ofd:PhysicalBox></ofd:PageArea></ofd:CommonData>
            <ofd:Pages><ofd:Page ID="1" BaseLoc="Pages/Page_0/Content.xml"/><ofd:Page ID="2" BaseLoc="Pages/Page_1/Content.xml"/></ofd:Pages></ofd:Document>
            """);
        Text(zip, "Doc_0/Res.xml", $"""<?xml version="1.0" encoding="utf-8"?><ofd:Res {Ns}><ofd:Fonts><ofd:Font ID="3" FontName="仿宋_GB2312"/><ofd:Font ID="4" FontName="黑体"/></ofd:Fonts></ofd:Res>""");

        string Line(string text, double x, double y, int font = 3, double size = 5.6, string extra = "") =>
            $"""<ofd:TextObject ID="1" Font="{font}" Size="{size}" Boundary="{x} {y - size} 160 {size * 1.3}"{extra}><ofd:FillColor Value="5 7 59"/><ofd:TextCode X="0" Y="{size}">{text}</ofd:TextCode></ofd:TextObject>""";
        var first = new StringBuilder();
        first.Append(Line("医院信息化建设方案", 67.15, 40, 4, 8.5, " Weight=\"700\""));
        // A paragraph of three lines on a 156.8 mm measure (28 characters): the first indented by two characters.
        first.Append(Line(Body[..26], 38.2, 60));
        first.Append(Line(Body[26..54], 27, 70));
        first.Append(Line(Body[54..], 27, 80));
        first.Append(Line("第一章 总体规划", 27, 100, 4));
        Text(zip, "Doc_0/Pages/Page_0/Content.xml", $"""<?xml version="1.0" encoding="utf-8"?><ofd:Page {Ns}><ofd:Content><ofd:Layer ID="1">{first}</ofd:Layer></ofd:Content></ofd:Page>""");
        Text(zip, "Doc_0/Pages/Page_1/Content.xml", $"""<?xml version="1.0" encoding="utf-8"?><ofd:Page {Ns}><ofd:Content><ofd:Layer ID="1">{Line("第二章 实施步骤", 27, 40, 4)}</ofd:Layer></ofd:Content></ofd:Page>""");
        return path;
    }

    [Fact]
    public void Wps_document_keeps_paragraphs_pages_bold_and_colour()
    {
        var folder = fx.NewFolder();
        var document = OfdReader.Read(MakeWps(folder), Path.Combine(folder, "media"));
        var blocks = document.Sections.Single().Blocks.OfType<HParagraph>().ToList();
        Assert.Equal(["医院信息化建设方案", Body, "第一章 总体规划", "第二章 实施步骤"], blocks.Select(ParagraphText));

        var title = blocks[0];
        Assert.Equal(HAlign.Center, title.Format.Align);
        var titleRun = title.Inlines.OfType<HText>().Single().Format;
        Assert.True(titleRun.Bold);
        Assert.Equal("#05073B", titleRun.Color);
        Assert.Equal("SimHei", titleRun.EastAsianFont);

        // The body paragraph: justified, a first-line indent of two characters.
        var body = blocks[1];
        Assert.Equal(HAlign.Justify, body.Format.Align);
        Assert.Equal(Hwp(11.2), body.Format.FirstLine);
        Assert.Equal("FangSong_GB2312", body.Inlines.OfType<HText>().First().Format.Font);

        Assert.False(blocks[2].PageBreakBefore);
        Assert.True(blocks[3].PageBreakBefore);
    }

    // ───────────────────────── Hostile and refused packages ─────────────────────────

    [Fact]
    public void References_cannot_leave_the_package_and_huge_counts_are_capped()
    {
        var folder = fx.NewFolder();
        var path = Path.Combine(folder, "hostile.ofd");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Text(zip, "OFD.xml", $"""<?xml version="1.0"?><ofd:OFD {Ns}><ofd:DocBody><ofd:DocRoot>Doc_0/../Doc_0/./Document.xml</ofd:DocRoot></ofd:DocBody></ofd:OFD>""");
            Text(zip, "Doc_0/Document.xml", $"""
                <?xml version="1.0"?>
                <ofd:Document {Ns}><ofd:CommonData><ofd:PublicRes>../../../outside/Res.xml</ofd:PublicRes><ofd:DocumentRes>/Doc_0/Res.xml</ofd:DocumentRes></ofd:CommonData>
                <ofd:Pages><ofd:Page ID="1" BaseLoc="C:/Windows/win.ini"/><ofd:Page ID="2" BaseLoc="/Doc_0/Pages/Page_0/Content.xml"/></ofd:Pages></ofd:Document>
                """);
            // Pictures that climb out of the package, or name a file on the disk, are not found; a name in the package is.
            Text(zip, "Doc_0/Res.xml", $"""
                <?xml version="1.0"?>
                <ofd:Res {Ns}><ofd:MultiMedias>
                <ofd:MultiMedia ID="1" Type="Image"><ofd:MediaFile>../../../../evil.png</ofd:MediaFile></ofd:MultiMedia>
                <ofd:MultiMedia ID="2" Type="Image"><ofd:MediaFile>file:///C:/Windows/win.ini</ofd:MediaFile></ofd:MultiMedia>
                <ofd:MultiMedia ID="3" Type="Image"><ofd:MediaFile>/Doc_0/Res/../Res/ok.png</ofd:MediaFile></ofd:MultiMedia>
                </ofd:MultiMedias></ofd:Res>
                """);
            Entry(zip, "../evil.png", File.ReadAllBytes(EngineFixture.MakeImage(folder, "evil.png", MagickFormat.Png, 40, 40)));
            Entry(zip, "Doc_0/Res/ok.png", File.ReadAllBytes(EngineFixture.MakeImage(folder, "ok.png", MagickFormat.Png, 40, 40)));
            // A repeat count of two billion, a text object nested deeper than the reader follows.
            var nested = string.Concat(Enumerable.Repeat("<ofd:PageBlock>", 200)) + $"""<ofd:TextObject ID="9" Boundary="20 200 50 5" Size="4"><ofd:TextCode X="0" Y="4">太深</ofd:TextCode></ofd:TextObject>"""
                         + string.Concat(Enumerable.Repeat("</ofd:PageBlock>", 200));
            Text(zip, "Doc_0/Pages/Page_0/Content.xml", $"""
                <?xml version="1.0"?>
                <ofd:Page {Ns}><ofd:Content><ofd:Layer ID="1">
                <ofd:TextObject ID="4" Boundary="20 20 100 5" Size="4"><ofd:TextCode X="0" Y="4" DeltaX="g 2000000000 4">安全</ofd:TextCode></ofd:TextObject>
                <ofd:ImageObject ID="5" ResourceID="1" Boundary="20 40 10 10"/><ofd:ImageObject ID="6" ResourceID="2" Boundary="20 60 10 10"/>
                <ofd:ImageObject ID="7" ResourceID="3" Boundary="20 80 10 10"/>{nested}</ofd:Layer></ofd:Content></ofd:Page>
                """);
            // The same name twice: the first part wins, nothing throws.
            Text(zip, "doc_0/pages/page_0/content.xml", "not xml");
        }

        var media = Path.Combine(folder, "media");
        var document = OfdReader.Read(path, media);
        var paragraphs = document.Sections.Single().Blocks.OfType<HParagraph>().ToList();
        Assert.Equal("安全", ParagraphText(paragraphs[0]));
        Assert.Single(paragraphs.SelectMany(p => p.Inlines).OfType<HImage>());
        Assert.DoesNotContain(paragraphs, p => ParagraphText(p).Contains("太深"));
        Assert.Single(Directory.GetFiles(media));
        Assert.All(Directory.GetFiles(media), f => Assert.StartsWith(Path.GetFullPath(media), Path.GetFullPath(f)));
    }

    [Fact]
    public void Dtds_are_refused()
    {
        var folder = fx.NewFolder();
        var path = Path.Combine(folder, "xxe.ofd");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Text(zip, "OFD.xml", $"""
                <?xml version="1.0"?>
                <!DOCTYPE ofd:OFD [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;"><!ENTITY x SYSTEM "file:///C:/Windows/win.ini">]>
                <ofd:OFD {Ns}><ofd:DocBody><ofd:DocRoot>&b;&x;</ofd:DocRoot></ofd:DocBody></ofd:OFD>
                """);
        }
        var error = Assert.Throws<InvalidDataException>(() => OfdReader.Read(path, Path.Combine(folder, "media")));
        Assert.Contains("not valid XML", error.Message);
    }

    [Fact]
    public async Task Encrypted_and_broken_packages_fail_with_a_clear_message()
    {
        var folder = fx.NewFolder();
        var encrypted = Path.Combine(folder, "encrypted.ofd");
        using (var zip = ZipFile.Open(encrypted, ZipArchiveMode.Create))
        {
            Text(zip, "OFD.xml", $"""<?xml version="1.0"?><ofd:OFD {Ns}><ofd:DocBody><ofd:DocRoot>Doc_0/Document.xml</ofd:DocRoot></ofd:DocBody></ofd:OFD>""");
            Text(zip, "Encryptions.xml", """<?xml version="1.0"?><Encryptions><Encryption ID="1"/></Encryptions>""");
            Entry(zip, "Doc_0/Document.xml", [0x8F, 0x13, 0x77, 0x02, 0xC4]);
        }
        var job = await fx.ConvertAsync([encrypted], new Preset { TargetFormat = "docx" });
        Assert.Equal(JobState.Failed, job.State);
        Assert.Contains("encrypted", job.Files.Single().ErrorDetail, StringComparison.OrdinalIgnoreCase);

        var notZip = Path.Combine(folder, "plain.ofd");
        await File.WriteAllTextAsync(notZip, "<OFD/>", TestContext.Current.CancellationToken);
        Assert.Contains("not an OFD document", Assert.Throws<InvalidDataException>(() => OfdReader.Read(notZip, folder)).Message);

        var empty = Path.Combine(folder, "empty.ofd");
        using (var zip = ZipFile.Open(empty, ZipArchiveMode.Create))
            Text(zip, "readme.txt", "no OFD.xml here");
        Assert.Contains("no OFD.xml", Assert.Throws<InvalidDataException>(() => OfdReader.Read(empty, folder)).Message);
    }

    // ───────────────────────── Helpers ─────────────────────────

    private static int Hwp(double mm) => (int)Math.Round(mm * 7200 / 25.4);

    private static string ParagraphText(HParagraph paragraph) =>
        string.Concat(paragraph.Inlines.Select(i => i switch
        {
            HText text => text.Text,
            HTab => "\t",
            HLineBreak => "\n",
            _ => "",
        }));

    private static void Text(ZipArchive zip, string name, string content) => Entry(zip, name, Encoding.UTF8.GetBytes(content));

    private static void Entry(ZipArchive zip, string name, byte[] bytes)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(bytes);
    }
}
