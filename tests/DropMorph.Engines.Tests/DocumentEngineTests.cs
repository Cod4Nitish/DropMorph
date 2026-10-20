// Tests for engines that are downloaded or bundled separately (LibreOffice, rhwp).
// They skip automatically when the engine is not available, so CI stays green without them.

using System.IO.Compression;
using DropMorph.Core.Conversion;
using DropMorph.Core.Presets;
using DropMorph.Engines.Hwp;
using DropMorph.Engines.Office;

namespace DropMorph.Engines.Tests;

public class DocumentEngineTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    /// <summary>The engine with this id; skips the test when it is not registered.</summary>
    private IConverter Engine(string id)
    {
        var engine = fx.Converters.SingleOrDefault(c => c.Id == id);
        Assert.SkipUnless(engine is not null, $"{id} not registered");
        return engine!;
    }

    private void SkipUnlessAvailable(string id)
    {
        var status = fx.Catalog.StatusOf(Engine(id));
        Assert.SkipUnless(status.IsAvailable, $"{id} not available ({status.ReasonKey})");
    }

    [Theory]
    [InlineData("docx", "pdf", false, "pdf:writer_pdf_Export")]
    [InlineData("xlsx", "pdf", false, "pdf:calc_pdf_Export")]
    [InlineData("docx", "pdf", true, "pdf:writer_pdf_Export:{\"SelectPdfVersion\":{\"type\":\"long\",\"value\":\"2\"}}")]
    [InlineData("odt", "docx", false, "docx:MS Word 2007 XML")]
    [InlineData("xlsx", "html", false, "html:HTML (StarCalc)")]
    public void LibreOffice_filter_names(string from, string to, bool pdfA, string expected) =>
        Assert.Equal(expected, LibreOfficeConverter.FilterFor(from, to, pdfA));

    [Fact]
    public async Task LibreOffice_txt_to_docx_and_pdf()
    {
        SkipUnlessAvailable("libreoffice");
        var dir = fx.NewFolder();
        var txt = Path.Combine(dir, "note.txt");
        await File.WriteAllTextAsync(txt, "DropMorph 변환 테스트\n文件转换测试\nHello", TestContext.Current.CancellationToken);

        var docx = await fx.ConvertAsync([txt], new Preset { TargetFormat = "docx" });
        Assert.Equal(JobState.Completed, docx.State);

        var pdf = await fx.ConvertAsync([docx.Outputs.Single()], new Preset { TargetFormat = "pdf" });
        Assert.Equal(JobState.Completed, pdf.State);
        Assert.True(new FileInfo(pdf.Outputs.Single()).Length > 1000);
    }

    [Fact]
    public async Task Hwpx_to_pdf()
    {
        Assert.SkipUnless(fx.Converters.Any(c => c.Id is "rhwp" or "libreoffice" && fx.Catalog.StatusOf(c).IsAvailable), "no HWPX renderer");
        var hwpx = await fx.MakeHwpxAsync(fx.NewFolder());

        var job = await fx.ConvertAsync([hwpx], new Preset { TargetFormat = "pdf" });

        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        Assert.True(new FileInfo(job.Outputs.Single()).Length > 1000);
    }

    [Fact]
    public async Task Hwpx_to_text()
    {
        SkipUnlessAvailable("unhwp");
        var hwpx = await fx.MakeHwpxAsync(fx.NewFolder());

        var job = await fx.ConvertAsync([hwpx], new Preset { TargetFormat = "txt" });

        Assert.Equal(JobState.Completed, job.State);
        Assert.Contains("두 번째 문단입니다", await File.ReadAllTextAsync(job.Outputs.Single(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Hwpx_to_markdown_and_html_keeps_the_pictures()
    {
        SkipUnlessAvailable("unhwp");
        var dir = fx.NewFolder();
        EngineFixture.MakeImage(dir, "chart.png", ImageMagick.MagickFormat.Png);
        var source = Path.Combine(dir, "memo.md");
        await File.WriteAllTextAsync(source, "# 회의 메모\n\n그림 앞 문단.\n\n![](chart.png)\n\n그림 뒤 문단.\n", TestContext.Current.CancellationToken);
        var hwpx = (await fx.ConvertAsync([source], new Preset { TargetFormat = "hwpx" })).Outputs.Single();
        Assert.Equal("unhwp", Assert.Single(fx.Catalog.CreatePlanner().Plan("hwpx", "md")!.Steps).Converter.Id);

        // Unhwp links "assets/image1.png" without writing it: the picture goes into "memo_files" next to the result.
        var md = (await fx.ConvertAsync([hwpx], new Preset { TargetFormat = "md" })).Outputs.Single();
        var markdown = await File.ReadAllTextAsync(md, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("assets/", markdown);
        var link = System.Text.RegularExpressions.Regex.Match(markdown, @"!\[[^\]]*\]\(<([^>]+)>\)").Groups[1].Value;
        Assert.StartsWith("memo_files/", link, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(dir, Uri.UnescapeDataString(link))));
        Assert.Contains("그림 뒤 문단", markdown);

        // HTML is one page: the picture is embedded.
        var html = await File.ReadAllTextAsync((await fx.ConvertAsync([hwpx], new Preset { TargetFormat = "html" })).Outputs.Single(), TestContext.Current.CancellationToken);
        Assert.Contains("src=\"data:image/png;base64,", html);
        Assert.DoesNotContain("assets/", html);

        // Text only: no picture, no folder.
        var folders = Directory.GetDirectories(dir).Length;
        var textOnly = await File.ReadAllTextAsync((await fx.ConvertAsync([hwpx], new Preset { TargetFormat = "md", Document = { LeaveOutPictures = true } })).Outputs.Single(), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("![", textOnly);
        Assert.Contains("그림 앞 문단.\n\n그림 뒤 문단", textOnly.ReplaceLineEndings("\n"));
        Assert.Equal(folders, Directory.GetDirectories(dir).Length);
    }

    [Fact]
    public async Task Rhwp_converts_between_hwpx_and_hwp()
    {
        SkipUnlessAvailable("rhwp");
        var hwpx = await fx.MakeHwpxAsync(fx.NewFolder());

        var hwp = await fx.ConvertAsync([hwpx], new Preset { TargetFormat = "hwp" });
        Assert.True(hwp.State == JobState.Completed, string.Join("; ", hwp.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        // HWP 5.0 is an OLE compound file.
        var header = new byte[8];
        await using (var stream = File.OpenRead(hwp.Outputs.Single()))
            await stream.ReadExactlyAsync(header, TestContext.Current.CancellationToken);
        Assert.Equal([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1], header);

        var back = await fx.ConvertAsync([hwp.Outputs.Single()], new Preset { TargetFormat = "hwpx" });
        Assert.Equal(JobState.Completed, back.State);
        Assert.Equal("rhwp", Assert.Single(fx.Catalog.CreatePlanner().Plan("hwp", "hwpx")!.Steps).Converter.Id);
        Assert.Contains("두 번째 문단입니다", HwpxAssert.ReadBackText(back.Outputs.Single()));
    }

    [Fact]
    public async Task Rhwp_time_limit_grows_with_the_documents_text()
    {
        var dir = fx.NewFolder();
        var hwpx = await fx.MakeHwpxAsync(dir);
        using (var zip = ZipFile.OpenRead(hwpx))
            Assert.Equal(zip.Entries.Where(e => e.FullName.StartsWith("Contents/section", StringComparison.Ordinal)).Sum(e => e.Length), RhwpConverter.TextSize(hwpx));
        var hwp = Path.Combine(dir, "deflated.hwp");
        await File.WriteAllBytesAsync(hwp, new byte[1000], TestContext.Current.CancellationToken);
        Assert.Equal(3000, RhwpConverter.TextSize(hwp)); // not a ZIP: an HWP's text is deflated to about a third

        Assert.Equal(TimeSpan.FromMinutes(2), RhwpConverter.TimeLimit(0));
        Assert.Equal(TimeSpan.FromMinutes(32), RhwpConverter.TimeLimit(1024 * 1024));
        Assert.Equal(TimeSpan.FromMinutes(60), RhwpConverter.TimeLimit(100 * 1024 * 1024)); // a hung rhwp is still stopped
    }

    [Fact]
    public async Task LibreOffice_with_H2Orestart_reads_hwpx()
    {
        SkipUnlessAvailable("libreoffice");
        var libreOffice = Engine("libreoffice");
        Assert.SkipUnless(libreOffice.Edges.Any(e => e.From == "hwpx"), "H2Orestart extension not installed");
        var dir = fx.NewFolder();
        var hwpx = await fx.MakeHwpxAsync(dir);
        var output = Path.Combine(dir, "from-hwpx.docx");

        // Call the engine directly: the planner may prefer another HWPX reader.
        var written = await libreOffice.ConvertAsync(
            new ConversionStep(hwpx, "hwpx", "docx", new Preset(), new SinglePath(output), dir), null, TestContext.Current.CancellationToken);

        Assert.Equal(output, written.Single());
        Assert.True(new FileInfo(output).Length > 2000);
    }

    private sealed class SinglePath(string path) : IOutputAllocator
    {
        public string? Allocate(string extension, string? suffix = null) => path;
    }
}
