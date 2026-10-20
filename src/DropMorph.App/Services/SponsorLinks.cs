// Where "Sponsor" can lead: the sidebar button and the About page open a window with these.

namespace DropMorph.App.Services;

/// <summary>One way to support DropMorph.</summary>
/// <param name="Id">Also the suffix of its description string, <c>sponsor.note.{Id}</c>.</param>
/// <param name="Icon">Key of the logo geometry (Styles/Brands.axaml, or Icon.Heart).</param>
/// <param name="Color">Brand colour behind the white logo.</param>
public sealed record SponsorLink(string Id, string Name, string Url, string Icon, string Color);

public static class SponsorLinks
{
    public const string GitHubSponsorsUrl = "https://github.com/sponsors/cod4nitish";

    private static readonly SponsorLink GitHub = new("github", "GitHub Sponsors", GitHubSponsorsUrl, "Brand.GitHubSponsors", "#BF3989");

    /// <summary>Every way to support DropMorph. The same for every language.</summary>
    public static IReadOnlyList<SponsorLink> For(string language) => [GitHub];
}
