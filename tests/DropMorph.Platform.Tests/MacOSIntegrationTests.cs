using System.Xml.Linq;
using DropMorph.Platform.MacOS;

namespace DropMorph.Platform.Tests;

public sealed class MacOSIntegrationTests
{
    [Fact]
    public void LaunchAgentKeepsExecutableAndArgumentsSeparate()
    {
        const string executable = "/Users/a & b/도구 '$(touch nope)'/DropMorph.app/Contents/MacOS/DropMorph";
        var root = ReadPlist(MacOSIntegrationFiles.LaunchAgent(executable));
        Assert.Equal([executable, "--background"], Property(root, "ProgramArguments").Elements().Select(x => x.Value));
        Assert.Equal("true", Property(root, "RunAtLoad").Name.LocalName);
        Assert.Equal("false", Property(root, "KeepAlive").Name.LocalName);
        Assert.Equal("Aqua", Property(root, "LimitLoadToSessionType").Value);
    }

    [Fact]
    public void ServiceSwitchWritesTheEntrySystemSettingsUses()
    {
        Assert.Equal("\"com.cod4nitish.dropmorph - Convert with DropMorph - convertFiles\"", MacOSIntegrationFiles.ServiceStatusKey);
        Assert.Contains("\"enabled_context_menu\" = 0;", MacOSIntegrationFiles.ServiceStatus(false));
        Assert.Contains("\"ContextMenu\" = 1;", MacOSIntegrationFiles.ServiceStatus(true));
    }

    [Fact]
    public void AppBundleDeclaresTheServiceDropMorphAnswers()
    {
        var installer = Path.Combine(RepositoryRoot(), "installer", "macos");
        var root = ReadPlist(File.ReadAllText(Path.Combine(installer, "Info.plist")));
        var service = Property(root, "NSServices").Element("dict")!;
        Assert.Equal(MacOSServicesProvider.MenuItem, Property(Property(service, "NSMenuItem"), "default").Value);
        Assert.Equal(MacOSServicesProvider.Message, Property(service, "NSMessage").Value);
        Assert.Equal("DropMorph", Property(service, "NSPortName").Value);
        Assert.Equal("public.item", Property(service, "NSSendFileTypes").Element("string")!.Value);
        // Each translation names the same menu title.
        foreach (var strings in Directory.GetFiles(installer, "ServicesMenu.strings", SearchOption.AllDirectories))
            Assert.StartsWith($"\"{MacOSServicesProvider.MenuItem}\" = ", File.ReadAllLines(strings).Single(l => l.StartsWith('"')));
        // Every language in CFBundleLocalizations has a translation, and there is none for a language it doesn't list.
        var languages = Property(root, "CFBundleLocalizations").Elements("string").Select(x => x.Value).Order();
        var translated = Directory.GetDirectories(installer, "*.lproj").Select(x => Path.GetFileNameWithoutExtension(x)!).Order();
        Assert.Equal(languages, translated);
        foreach (var language in languages)
            Assert.True(File.Exists(Path.Combine(installer, language + ".lproj", "ServicesMenu.strings")), language);
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "DropMorph.slnx")))
            folder = folder.Parent;
        return folder?.FullName ?? throw new DirectoryNotFoundException("DropMorph.slnx not found above the test output.");
    }

    [Fact]
    public void SelectionJsonPreservesWhitespaceUnicodeAndNewlines()
    {
#pragma warning disable CA1416 // Pure JSON parsing does not access the operating system.
        var paths = MacOSPlatformServices.ParseSelection("[\"/tmp/ 한글 .txt\",\"/tmp/line\\nbreak.txt\",\"/tmp/quote\\\".txt\"]");
#pragma warning restore CA1416
        Assert.Equal(["/tmp/ 한글 .txt", "/tmp/line\nbreak.txt", "/tmp/quote\".txt"], paths);
    }

    private static XElement ReadPlist(string text)
    {
        var document = XDocument.Parse(text);
        Assert.Equal("plist", document.Root!.Name.LocalName);
        Assert.Equal("1.0", document.Root.Attribute("version")!.Value);
        return document.Root.Element("dict")!;
    }

    private static XElement Property(XElement dictionary, string key) =>
        dictionary.Elements("key").Single(x => x.Value == key).ElementsAfterSelf().First();
}
