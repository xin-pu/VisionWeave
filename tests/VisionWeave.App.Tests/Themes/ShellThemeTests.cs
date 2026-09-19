using System.Xml.Linq;
using Shouldly;

namespace VisionWeave.App.Tests.Themes;

/// <summary>
/// Holds the shell's semantic theme to the dark-amber direction: the documented
/// token values, one brush per token, the framework keys aliased onto the tokens,
/// and the contrast ratios the direction requires of text and of keyboard focus.
/// The markup is read as data rather than loaded into a window, so the whole theme
/// is covered without a dispatcher or an STA thread.
/// </summary>
public sealed class ShellThemeTests
{
    /// <summary>The documented values of the semantic tokens.</summary>
    private static readonly (string Token, string Value)[] DocumentedTokens =
    [
        ("Color.Background.Canvas", "#FF111315"),
        ("Color.Background.Surface", "#FF1A1D20"),
        ("Color.Background.SurfaceRaised", "#FF24282D"),
        ("Color.Border.Subtle", "#FF363B42"),
        ("Color.Text.Primary", "#FFF5F2EA"),
        ("Color.Text.Secondary", "#FFB9B5AC"),
        ("Color.Accent.Primary", "#FFF6B73C"),
        ("Color.Accent.Hover", "#FFFFD166"),
        ("Color.Accent.Pressed", "#FFD99717"),
        ("Color.State.Success", "#FF69C48B"),
        ("Color.State.Warning", "#FFF6B73C"),
        ("Color.State.Danger", "#FFFF7070"),
        ("Color.State.Info", "#FF79B8FF"),
    ];

    /// <summary>
    /// The one token the direction does not list and the shell needs: text painted
    /// on an accent surface, which is also a control surface here.
    /// </summary>
    private const string OnAccentToken = "Color.Text.OnAccent";

    /// <summary>
    /// The framework keys the shell relies on, each pointing at a token instead of
    /// at a value of its own, so framework controls join the palette without any
    /// control template of ours changing.
    /// </summary>
    private static readonly (string Framework, string Token)[] FrameworkAliases =
    [
        ("ApplicationBackgroundBrush", "Color.Background.Canvas"),
        ("TextFillColorPrimaryBrush", "Color.Text.Primary"),
        ("TextFillColorSecondaryBrush", "Color.Text.Secondary"),
        ("ControlStrokeColorDefaultBrush", "Color.Border.Subtle"),
        ("ControlFillColorDefaultBrush", "Color.Background.SurfaceRaised"),
        ("AccentFillColorDefaultBrush", "Color.Accent.Primary"),
        ("AccentFillColorSecondaryBrush", "Color.Accent.Hover"),
        ("AccentFillColorTertiaryBrush", "Color.Accent.Pressed"),
        ("TextOnAccentFillColorPrimaryBrush", OnAccentToken),
    ];

    /// <summary>The ratio the direction requires of normal-size text.</summary>
    private const double TextRatio = 4.5;

    /// <summary>The ratio the direction requires of an indicator such as keyboard focus.</summary>
    private const double FocusRatio = 3.0;

    [Fact]
    public void Every_documented_token_carries_its_documented_value()
    {
        IReadOnlyDictionary<string, string> tokens = Tokens();

        foreach ((string token, string value) in DocumentedTokens)
        {
            tokens[token].ShouldBe(value, $"{token} must keep the value the visual direction documents.");
        }

        // A colour that no documented token names is a value the theme cannot
        // account for, so the dictionary may hold the documented set and nothing more.
        tokens.Keys.ShouldBe(
            [.. DocumentedTokens.Select(entry => entry.Token), OnAccentToken],
            ignoreOrder: true);
    }

    [Fact]
    public void Every_token_has_a_brush_that_reads_it()
    {
        Dictionary<string, XElement> brushes = BrushElements();

        foreach (string token in Tokens().Keys)
        {
            string name = BrushName(token);

            brushes.ShouldContainKey(name, $"the token {token} must be reachable through a brush.");
            ((string?)brushes[name].Attribute("Color")).ShouldBe(
                $"{{StaticResource {token}}}",
                $"{name} must read the token rather than repeat its value.");
        }

        // Nothing else may declare itself a colour brush: a brush beyond the tokens
        // and the framework aliases would be a value no token controls.
        brushes.Keys.ShouldBe(
            [
                .. Tokens().Keys.Select(BrushName),
                .. FrameworkAliases.Select(alias => alias.Framework),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void The_framework_theme_keys_are_aliased_onto_the_tokens()
    {
        Dictionary<string, XElement> brushes = BrushElements();
        IReadOnlyDictionary<string, string> tokens = Tokens();

        foreach ((string framework, string token) in FrameworkAliases)
        {
            brushes.ShouldContainKey(framework, $"the framework theme key {framework} must be aliased.");
            ((string?)brushes[framework].Attribute("Color")).ShouldBe(
                $"{{StaticResource {token}}}",
                $"{framework} must follow the token {token}.");
            tokens.ShouldContainKey(token);
        }
    }

    [Fact]
    public void Every_state_and_text_colour_meets_the_contrast_ratio_on_every_surface()
    {
        IReadOnlyDictionary<string, string> tokens = Tokens();
        string[] surfaces =
        [
            tokens["Color.Background.Canvas"],
            tokens["Color.Background.Surface"],
            tokens["Color.Background.SurfaceRaised"],
        ];

        // Every colour the shell paints text with: the two text tokens, the accent
        // it uses for a catalogue heading, and the four state colours a condition is
        // reported in. The state colours are also why the severity is named in words:
        // colour alone would be the only difference between them for a reader who
        // cannot separate amber from blue.
        string[] texts =
        [
            tokens["Color.Text.Primary"],
            tokens["Color.Text.Secondary"],
            tokens["Color.Accent.Primary"],
            tokens["Color.State.Success"],
            tokens["Color.State.Warning"],
            tokens["Color.State.Danger"],
            tokens["Color.State.Info"],
        ];

        foreach (string text in texts)
        {
            foreach (string surface in surfaces)
            {
                Contrast(text, surface).ShouldBeGreaterThanOrEqualTo(
                    TextRatio,
                    $"{text} must be readable on {surface}.");
            }
        }
    }

    [Fact]
    public void Text_on_an_accent_surface_meets_the_contrast_ratio()
    {
        IReadOnlyDictionary<string, string> tokens = Tokens();

        foreach (string accent in new[]
        {
            tokens["Color.Accent.Primary"],
            tokens["Color.Accent.Hover"],
            tokens["Color.Accent.Pressed"],
        })
        {
            Contrast(tokens[OnAccentToken], accent).ShouldBeGreaterThanOrEqualTo(
                TextRatio,
                $"text painted on {accent} must be readable there.");
        }
    }

    [Fact]
    public void The_focus_ring_meets_the_non_text_contrast_ratio()
    {
        IReadOnlyDictionary<string, string> tokens = Tokens();
        string accent = tokens["Color.Accent.Primary"];

        // The ring is drawn just outside the control it belongs to, so it is held to
        // what the direction requires of a focus indicator against every surface it
        // can end up on.
        foreach (string surface in new[]
        {
            tokens["Color.Background.Canvas"],
            tokens["Color.Background.Surface"],
            tokens["Color.Background.SurfaceRaised"],
        })
        {
            Contrast(accent, surface).ShouldBeGreaterThanOrEqualTo(
                FocusRatio,
                $"the focus ring must be visible against {surface}.");
        }
    }

    [Fact]
    public void The_token_dictionary_is_the_only_file_that_names_a_colour()
    {
        // A literal anywhere else is a value the theme cannot replace, which is the
        // one thing semantic tokens exist to prevent.
        foreach (string file in new[] { "App.xaml", "MainWindow.xaml", "Themes/Shell.xaml" })
        {
            ColourLiterals(Read(file)).ShouldBeEmpty($"{file} must name a token instead of a colour.");
        }
    }

    private static string BrushName(string token)
        => token.Replace("Color.", "Brush.", StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> Tokens()
        => Load("Themes/Tokens.xaml")
            .Root!
            .Elements()
            .Where(element => element.Name.LocalName == "Color")
            .ToDictionary(
                element => (string)element.Attribute(Xaml + "Key")!,
                element => element.Value.Trim());

    private static Dictionary<string, XElement> BrushElements()
        => Load("Themes/Tokens.xaml")
            .Root!
            .Elements()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .ToDictionary(element => (string)element.Attribute(Xaml + "Key")!);

    private static IEnumerable<string> ColourLiterals(string markup)
        => System.Text.RegularExpressions.Regex
            .Matches(markup, "#[0-9A-Fa-f]{6,8}")
            .Select(match => match.Value);

    /// <summary>
    /// Computes the contrast ratio of two opaque colours, using the relative
    /// luminance the accessibility guidelines define.
    /// </summary>
    private static double Contrast(string foreground, string background)
    {
        double first = Luminance(foreground);
        double second = Luminance(background);

        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(string colour)
    {
        (int red, int green, int blue) = Channels(colour);

        return (0.2126 * Channel(red)) + (0.7152 * Channel(green)) + (0.0722 * Channel(blue));
    }

    private static double Channel(int value)
    {
        double channel = value / 255.0;

        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static (int Red, int Green, int Blue) Channels(string colour)
    {
        string digits = colour.TrimStart('#');

        if (digits.Length is not (6 or 8))
        {
            throw new FormatException($"{colour} must be written as #RRGGBB or #AARRGGBB.");
        }

        // The alpha channel is dropped: every token the shell paints with is opaque.
        string rgb = digits.Length == 8 ? digits[2..] : digits;

        return (
            Convert.ToInt32(rgb[..2], 16),
            Convert.ToInt32(rgb[2..4], 16),
            Convert.ToInt32(rgb[4..], 16));
    }

    private static XDocument Load(string relativePath) => XDocument.Load(PathOf(relativePath));

    private static string Read(string relativePath) => System.IO.File.ReadAllText(PathOf(relativePath));

    /// <summary>
    /// Resolves a shell file the test project links into its output, so the theme is
    /// checked against the markup the application actually ships.
    /// </summary>
    private static string PathOf(string relativePath)
        => System.IO.Path.Combine(AppContext.BaseDirectory, "Shell", relativePath);

    private static XNamespace Xaml => "http://schemas.microsoft.com/winfx/2006/xaml";
}
