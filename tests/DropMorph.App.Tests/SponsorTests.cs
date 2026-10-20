// "Sponsor" opens a window with every way to support DropMorph: GitHub Sponsors, in every language.

using Avalonia.Headless.XUnit;
using Avalonia.Media;
using DropMorph.App.Services;
using DropMorph.App.Views;
using DropMorph.Core.Localization;

namespace DropMorph.App.Tests;

public class SponsorTests
{
    [AvaloniaTheory]
    [InlineData("ko", SponsorLinks.GitHubSponsorsUrl)]
    [InlineData("en", SponsorLinks.GitHubSponsorsUrl)]
    [InlineData("zh-CN", SponsorLinks.GitHubSponsorsUrl)]
    public void GitHub_Sponsors_is_the_way_in_every_language(string language, string url)
    {
        var ways = SponsorLinks.For(language);

        Assert.Equal(url, ways[0].Url);
        Assert.Single(ways);
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("ko")]
    [InlineData("zh-CN")]
    public void Every_way_has_its_logo_and_note(string language)
    {
        TestServices.EnsureInitialized(language);
        var window = SponsorWindow.Create(AppHost.Get<ILocalizer>());

        var ways = Assert.IsAssignableFrom<IEnumerable<SponsorWay>>(window.Ways.ItemsSource).ToList();
        Assert.Single(ways);
        Assert.All(ways, w =>
        {
            Assert.IsAssignableFrom<Geometry>(w.Icon);
            Assert.False(string.IsNullOrWhiteSpace(w.Note));
            Assert.DoesNotContain("sponsor.note.", w.Note);
        });
    }
}
