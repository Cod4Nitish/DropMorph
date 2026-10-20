// Applies ThemeSettings (light/dark, accent colour, corner radius, font order, reduced motion) and the user's
// theme.json colours at runtime by writing resources into Application.Resources. All styles reference these with
// DynamicResource.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using DropMorph.Core.Platform;
using DropMorph.Core.Settings;

namespace DropMorph.App.Services;

/// <summary>Runtime theming.</summary>
public sealed class ThemeService(IPlatformServices platform)
{
    /// <summary>Accent swatches offered in the Theme page.</summary>
    public static readonly IReadOnlyList<string> Swatches =
        ["#0D9488", "#FF6B35", "#7F77DD", "#378ADD", "#D4537E", "#BA7517", "#639922", "#5F5E5A"];

    /// <summary>Embedded Noto fonts (see build/fetch-fonts.ps1). Missing files fall back to system fonts.</summary>
    private const string Latin = "fonts:DropMorph#Noto Sans";
    private const string Korean = "fonts:DropMorph#Noto Sans KR";
    private const string Chinese = "fonts:DropMorph#Noto Sans SC";
    private const string Devanagari = "fonts:DropMorph#Noto Sans Devanagari";
    private const string SystemFallbacks =
        "Segoe UI, Nirmala UI, Malgun Gothic, Microsoft YaHei, PingFang SC, Apple SD Gothic Neo, Kohinoor Devanagari, sans-serif";

    /// <summary>
    /// theme.json colour names → the brushes in Styles/Soft.axaml they replace. Must cover exactly
    /// <see cref="CustomTheme.Keys"/> (a test checks).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> CustomBrushKeys = new Dictionary<string, string>
    {
        ["page"] = "PageBrush",
        ["surface"] = "SurfaceBrush",
        ["surfaceAlt"] = "SurfaceAltBrush",
        ["border"] = "SubtleBorderBrush",
        ["text"] = "TextPrimaryBrush",
        ["textSecondary"] = "TextSecondaryBrush",
        ["danger"] = "DangerBrush",
        ["success"] = "SuccessBrush",
        ["warning"] = "WarningBrush",
    };

    public void Apply(ThemeSettings theme, string language, CustomTheme? custom = null)
    {
        var app = Application.Current ?? throw new InvalidOperationException("No application.");
        var res = app.Resources;

        app.RequestedThemeVariant = theme.Mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        // Accent colour + derived shades. Fluent controls read the System* keys.
        var accent = Color.TryParse(theme.Accent, out var parsed) ? parsed : Color.Parse("#0D9488");
        res["AccentColor"] = accent;
        res["AccentBrush"] = new SolidColorBrush(accent);
        res["AccentSoftBrush"] = new SolidColorBrush(accent, 0.18);
        res["AccentFaintBrush"] = new SolidColorBrush(accent, 0.09);
        res["AccentStrongBrush"] = new SolidColorBrush(Shade(accent, -0.25));
        res["OnAccentBrush"] = new SolidColorBrush(Luminance(accent) > 0.6 ? Color.Parse("#1F1E1D") : Colors.White);
        res["SystemAccentColor"] = accent;
        res["SystemAccentColorLight1"] = Shade(accent, 0.15);
        res["SystemAccentColorLight2"] = Shade(accent, 0.30);
        res["SystemAccentColorLight3"] = Shade(accent, 0.45);
        res["SystemAccentColorDark1"] = Shade(accent, -0.15);
        res["SystemAccentColorDark2"] = Shade(accent, -0.30);
        res["SystemAccentColorDark3"] = Shade(accent, -0.45);

        // "Softness": one radius drives every control.
        var r = Math.Clamp(theme.CornerRadius, 0, 28);
        res["SoftRadius"] = new CornerRadius(r);
        res["SoftRadiusSmall"] = new CornerRadius(Math.Round(r * 0.55));
        res["SoftRadiusLarge"] = new CornerRadius(Math.Round(r * 1.35));
        res["ControlCornerRadius"] = new CornerRadius(Math.Round(r * 0.55));
        res["OverlayCornerRadius"] = new CornerRadius(r);

        // Han characters look different in Korean and Chinese fonts: put the UI language's font first.
        var cjk = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? $"{Chinese}, {Korean}"
            : $"{Korean}, {Chinese}";
        res["AppFontFamily"] = FontFamily.Parse($"{Latin}, {Devanagari}, {cjk}, {SystemFallbacks}");

        // Application.Resources is searched before the styles, so these theme dictionaries win over Soft.axaml's.
        ApplyCustom(res, ThemeVariant.Light, custom?.Light);
        ApplyCustom(res, ThemeVariant.Dark, custom?.Dark);

        Motion.Configure(theme.ReduceMotion, () => platform.PrefersReducedMotion);
    }

    /// <summary>Replaces (or, with no colours, removes) the user's colours for one variant.</summary>
    private static void ApplyCustom(IResourceDictionary res, ThemeVariant variant, IReadOnlyDictionary<string, string>? colors)
    {
        var dictionary = new ResourceDictionary();
        foreach (var (name, value) in colors ?? new Dictionary<string, string>())
            if (CustomBrushKeys.TryGetValue(name, out var key) && Color.TryParse(value, out var color))
                dictionary[key] = new SolidColorBrush(color);

        if (dictionary.Count == 0)
            res.ThemeDictionaries.Remove(variant);
        else
            res.ThemeDictionaries[variant] = dictionary;
    }

    /// <summary>Lightens (amount &gt; 0) or darkens (amount &lt; 0) a colour.</summary>
    public static Color Shade(Color c, double amount)
    {
        byte Mix(byte v) => amount >= 0
            ? (byte)Math.Round(v + (255 - v) * amount)
            : (byte)Math.Round(v * (1 + amount));
        return Color.FromArgb(c.A, Mix(c.R), Mix(c.G), Mix(c.B));
    }

    /// <summary>Relative luminance 0..1 (sRGB approximation).</summary>
    public static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
