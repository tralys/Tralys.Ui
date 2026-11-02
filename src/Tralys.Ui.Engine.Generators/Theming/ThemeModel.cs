using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Theming;

readonly record struct TokenFile(string Path, string Content);

// Read off the compilation, so the token compiler and the runtime never disagree on a vocabulary.
readonly record struct VocabularyEnum(string Name, EquatableArray<string> Members);

sealed class TokenSet(string resolver, Dictionary<string, string> sources)
{
    public string Resolver { get; } = resolver;

    public Dictionary<string, string> Sources { get; } = sources;
}

readonly record struct Argb(byte A, byte R, byte G, byte B)
{
    // The hex's own alpha and the token's alpha both apply.
    public static Argb From(uint argb, double alpha) =>
        new(
            (byte)Math.Round(Math.Max(0, Math.Min(1, alpha)) * (argb >> 24)),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb
        );

    // Opaque whatever this colour's alpha: a state layer composes over an opaque fill.
    public Argb Under(Argb above)
    {
        var a = above.A / 255f;
        return new Argb(255, Blend(R, above.R, a), Blend(G, above.G, a), Blend(B, above.B, a));
    }

    static byte Blend(byte below, byte above, float a)
    {
        float blended = below + (above - below) * a;
        return (byte)Math.Round((double)blended);
    }
}

readonly record struct PaletteEntry(
    string Group,
    string Variant,
    string State,
    Argb Background,
    Argb Foreground,
    Argb Border
);

sealed class DensityData(string member)
{
    public string Member { get; } = member;

    public double Spacing { get; set; }

    public List<(string Size, List<(string Leaf, double Value)> Leaves)> Sizes { get; } = [];

    public List<(string Variant, double Size)> FontSizes { get; } = [];

    public List<(string Token, double Radius)> Radii { get; } = [];

    public List<(string Token, double Size)> Grips { get; } = [];
}

sealed class ThemeData(string context, string member)
{
    public string Context { get; } = context;

    public string Member { get; } = member;

    public List<(string Token, Argb Color)> Colors { get; } = [];

    public List<(string Token, Argb Color)> ComponentColors { get; } = [];

    public List<PaletteEntry> Palettes { get; } = [];

    public List<PaletteEntry> Fields { get; } = [];

    public List<(string Token, string Family)> Fonts { get; } = [];

    public List<(string Token, int Weight)> Weights { get; } = [];

    public List<DensityData> Densities { get; } = [];

    public List<(string Token, double Seconds)> Durations { get; } = [];

    public List<(string Token, double[] Curve)> Easings { get; } = [];
}

sealed class TokenLayer(string label, TokenResolver resolver, Dictionary<string, string> sources)
{
    public string Label { get; } = label;

    public TokenResolver Resolver { get; } = resolver;

    public Dictionary<string, string> Sources { get; } = sources;

    public Dictionary<string, List<Token>> Parsed { get; } = new(StringComparer.Ordinal);

    public string? Context(string modifier, string context) =>
        Resolver.Modifiers.TryGetValue(modifier, out var declared)
        && !declared.Contexts.ContainsKey(context)
            ? declared.Default
            : context;
}

sealed class ThemeModel
{
    const string ThemeModifier = "theme";
    const string DensityModifier = "density";
    const string PaletteFolder = "palettes/";
    const int MaxAliasDepth = 16;
    const double NormalWeight = 400;

    static readonly string[] SizeLeaves =
    [
        "height",
        "pad-x",
        "pad-y",
        "field-pad-x",
        "gap",
        "font-size",
        "icon-size",
        "measure",
    ];

    static readonly string[] DimensionFamilies = ["size.", "text.", "radius.", "grip.", "spacing."];

    static readonly double[] Linear = [0, 0, 1, 1];

    readonly Dictionary<string, List<string>> _vocabulary;
    readonly List<string> _errors;
    readonly List<string> _warnings;

    ThemeModel(
        Dictionary<string, List<string>> vocabulary,
        List<string> errors,
        List<string> warnings
    )
    {
        _vocabulary = vocabulary;
        _errors = errors;
        _warnings = warnings;
    }

    public List<ThemeData> Themes { get; } = [];

    public string DefaultTheme { get; private set; } = "";

    public static ThemeModel? Build(
        TokenSet app,
        TokenSet tralys,
        Dictionary<string, List<string>> vocabulary,
        List<string> errors,
        List<string> warnings
    )
    {
        var model = new ThemeModel(vocabulary, errors, warnings);
        if (
            model.Layer(tralys, "Tralys.Ui's tokens: ") is not { } under
            || model.Layer(app, "") is not { } over
        )
            return null;

        model.Build([under, over]);
        return errors.Count == 0 ? model : null;
    }

    TokenLayer? Layer(TokenSet set, string label)
    {
        if (TokenResolver.Read(set.Resolver, out var error) is { } resolver)
            return new TokenLayer(label, resolver, set.Sources);

        _errors.Add($"{label}the resolver: {error}");
        return null;
    }

    void Build(TokenLayer[] layers)
    {
        if (
            Modifier(layers, ThemeModifier) is not { } themes
            || Modifier(layers, DensityModifier) is not { } densities
        )
            return;

        var densityContexts = new List<(string Member, string Context)>();
        foreach (var member in Members("Density"))
        {
            var context = Kebab(member);
            if (densities.Contexts.ContainsKey(context))
                densityContexts.Add((member, context));
            else
                _errors.Add(
                    $"the density modifier has no '{context}' context for Density.{member}"
                );
        }
        foreach (var context in densities.Contexts.Keys)
        {
            if (!densityContexts.Any(d => d.Context == context))
                _errors.Add(
                    $"the density modifier's '{context}' context is no member of Tralys.Ui's Density"
                );
        }
        if (_errors.Count > 0)
            return;

        foreach (var layer in layers)
            CheckDensitySetsDimensions(layer);

        DefaultTheme = themes.Default!;
        foreach (var context in themes.Contexts.Keys)
        {
            var member = Pascal(context);
            if (
                !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(member)
                || Themes.Any(t => t.Member == member)
            )
            {
                _errors.Add(
                    $"the theme context '{context}' does not name a distinct C# identifier"
                );
                continue;
            }

            var theme = new ThemeData(context, member);
            var flat = Flatten(layers, context, densities.Default!, out var palettePaths);
            if (flat is null)
                continue;

            ReadColors(theme, flat, palettePaths);
            ReadPalettes(theme);
            ReadTypography(theme, flat);
            ReadMotion(theme, flat);
            foreach (var (densityMember, densityContext) in densityContexts)
            {
                var dense =
                    densityContext == densities.Default
                        ? flat
                        : Flatten(layers, context, densityContext, out _);
                if (dense is null)
                    continue;

                theme.Densities.Add(ReadDimensions(context, densityMember, dense));
            }
            Themes.Add(theme);
        }
    }

    ResolverModifier? Modifier(TokenLayer[] layers, string name)
    {
        for (var i = layers.Length - 1; i >= 0; i--)
        {
            if (!layers[i].Resolver.Modifiers.TryGetValue(name, out var modifier))
                continue;

            if (modifier.Default is null || !modifier.Contexts.ContainsKey(modifier.Default))
            {
                _errors.Add(
                    $"{layers[i].Label}the '{name}' modifier's default is not one of its contexts"
                );
                return null;
            }
            return modifier;
        }

        _errors.Add($"the resolver declares no '{name}' modifier");
        return null;
    }

    Dictionary<string, Token>? Flatten(
        TokenLayer[] layers,
        string theme,
        string density,
        out HashSet<string> palettePaths
    )
    {
        palettePaths = new HashSet<string>(StringComparer.Ordinal);
        var flat = new Dictionary<string, Token>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            var inputs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ThemeModifier] = layer.Context(ThemeModifier, theme)!,
                [DensityModifier] = layer.Context(DensityModifier, density)!,
            };
            if (layer.Resolver.Order(inputs, out var error) is not { } order)
            {
                _errors.Add($"{layer.Label}the resolver: {error}");
                return null;
            }

            foreach (var source in order)
            {
                if (Parse(layer, source) is not { } tokens)
                {
                    _errors.Add(
                        $"{layer.Label}the resolver names '{source}', which is no token file beside it"
                    );
                    return null;
                }

                var fromPalette =
                    source.StartsWith(PaletteFolder, StringComparison.Ordinal)
                    || source.IndexOf("/" + PaletteFolder, StringComparison.Ordinal) >= 0;
                foreach (var token in tokens)
                {
                    flat[token.Path] = token;
                    if (fromPalette)
                        palettePaths.Add(token.Path);
                    else
                        palettePaths.Remove(token.Path);
                }
            }
        }
        return flat;
    }

    List<Token>? Parse(TokenLayer layer, string source)
    {
        if (layer.Parsed.TryGetValue(source, out var tokens))
            return tokens;
        if (!layer.Sources.TryGetValue(source, out var content))
            return null;
        return layer.Parsed[source] = TokenReader.Read(content, _errors, layer.Label + source);
    }

    void CheckDensitySetsDimensions(TokenLayer layer)
    {
        if (!layer.Resolver.Modifiers.TryGetValue(DensityModifier, out var densities))
            return;

        foreach (var context in densities.Contexts)
        {
            foreach (var source in context.Value)
            {
                foreach (var token in Parse(layer, source) ?? [])
                {
                    if (!IsDimension(token.Path))
                        _errors.Add(
                            $"{layer.Label}the {context.Key} density sets '{token.Path}'; a density sets dimensions alone"
                        );
                }
            }
        }
    }

    void ReadColors(ThemeData theme, Dictionary<string, Token> flat, HashSet<string> palettePaths)
    {
        var semantic = new Dictionary<string, string>(StringComparer.Ordinal);
        var component = new Dictionary<string, string>(StringComparer.Ordinal);
        var tokens = new HashSet<string>(Members("ColorToken"), StringComparer.Ordinal);
        var components = new HashSet<string>(Members("ComponentColor"), StringComparer.Ordinal);
        foreach (var path in flat.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (flat[path].Kind != TokenKind.Color)
                continue;

            var name = SlotName(path);
            if (path.StartsWith("component.", StringComparison.Ordinal))
            {
                if (components.Contains(name))
                    Claim(component, "ComponentColor", name, path);
                continue;
            }

            var escapeHatch = path == "color.transparent";
            if (
                !escapeHatch
                && (
                    !path.StartsWith("color.", StringComparison.Ordinal)
                    || palettePaths.Contains(path)
                )
            )
                continue;

            if (tokens.Contains(name))
                Claim(semantic, "ColorToken", name, path);
            else if (!escapeHatch)
                _errors.Add(
                    $"'{path}' is a semantic colour, but Tralys.Ui's ColorToken has no '{name}'; add it there or move the token out of color."
                );
        }

        foreach (var member in Members("ColorToken"))
            theme.Colors.Add((member, Color(theme, flat, semantic, "ColorToken", member)));
        foreach (var member in Members("ComponentColor"))
            theme.ComponentColors.Add(
                (member, Color(theme, flat, component, "ComponentColor", member))
            );
    }

    void Claim(Dictionary<string, string> found, string vocabulary, string name, string path)
    {
        if (found.TryGetValue(name, out var other))
            _errors.Add($"'{path}' and '{other}' both name {vocabulary}.{name}");
        else
            found[name] = path;
    }

    Argb Color(
        ThemeData theme,
        Dictionary<string, Token> flat,
        Dictionary<string, string> found,
        string vocabulary,
        string member
    )
    {
        if (!found.TryGetValue(member, out var path))
        {
            _warnings.Add(
                $"no token gives {vocabulary}.{member} in the {theme.Context} theme, so it is transparent"
            );
            return default;
        }
        return Resolve(flat, path, TokenKind.Color, out var alpha) is { Argb: { } argb }
            ? Argb.From(argb, alpha ?? 1)
            : default;
    }

    // The alias nearest the path that gives an alpha wins.
    Token? Resolve(Dictionary<string, Token> flat, string path, TokenKind kind, out double? alpha)
    {
        alpha = null;
        var at = path;
        for (var depth = 0; depth < MaxAliasDepth; depth++)
        {
            if (!flat.TryGetValue(at, out var token))
            {
                _errors.Add($"'{path}' aliases '{at}', which is no token");
                return null;
            }
            if (token.Kind != kind)
            {
                _errors.Add($"'{path}' resolves to '{at}', which is no {Lower(kind)}");
                return null;
            }
            alpha ??= token.Alpha;
            if (token.Alias is null)
                return token;
            at = token.Alias;
        }
        _errors.Add($"'{path}' aliases in a cycle");
        return null;
    }

    void ReadPalettes(ThemeData theme)
    {
        var colors = theme.Colors.ToDictionary(c => c.Token, c => c.Color, StringComparer.Ordinal);

        Argb Get(string token)
        {
            if (colors.TryGetValue(token, out var color))
                return color;
            _errors.Add(
                $"Tralys.Ui's ColorToken has no '{token}', which the palettes are built from"
            );
            return default;
        }

        var states = Members("ComponentState");
        foreach (var scheme in Members("ColorScheme"))
        {
            if (Schemes.FirstOrDefault(s => s.Name == scheme) is not { Name: not null } rule)
            {
                _errors.Add($"the token compiler has no palette rule for ColorScheme.{scheme}");
                continue;
            }

            var hoverLayer = Get("StateHover");
            var pressedLayer = Get("StatePressed");
            var transparent = Get("Transparent");
            var bg = Get(rule.Bg);
            var bgHover = rule.BgHover is { } hover ? Get(hover) : bg.Under(hoverLayer);
            var bgPressed = rule.BgPressed is { } pressed ? Get(pressed) : bg.Under(pressedLayer);
            var fg = Get(rule.Fg);
            var fgHover = Get(rule.FgHover);
            var border = Get(rule.Border);
            var borderHover = Get(rule.BorderHover);
            var muted = Get(rule.BgMuted);
            var washHover = rule.BgHover is null ? hoverLayer : muted;
            var washPressed = rule.BgPressed is null ? pressedLayer : muted;
            var filledFg = Get(rule.FilledFg);
            var filledBorder = rule.FilledBorder ? border : transparent;
            var filledBorderHover = rule.FilledBorder ? borderHover : transparent;

            foreach (var variant in Members("ComponentVariant"))
            {
                (Argb, Argb, Argb)[]? looks = variant switch
                {
                    "Filled" =>
                    [
                        (bg, filledFg, filledBorder),
                        (bgHover, filledFg, filledBorderHover),
                        (bgPressed, filledFg, filledBorder),
                    ],
                    "Outlined" =>
                    [
                        (transparent, fg, border),
                        (transparent, fgHover, borderHover),
                        (washPressed, fg, borderHover),
                    ],
                    "Ghost" =>
                    [
                        (transparent, fg, transparent),
                        (hoverLayer, fg, transparent),
                        (pressedLayer, fg, transparent),
                    ],
                    "GhostOutlined" =>
                    [
                        (transparent, fg, transparent),
                        (hoverLayer, fg, borderHover),
                        (pressedLayer, fg, transparent),
                    ],
                    "Text" =>
                    [
                        (transparent, fg, transparent),
                        (washHover, fgHover, transparent),
                        (washPressed, fg, transparent),
                    ],
                    "Subtle" =>
                    [
                        (muted, fg, transparent),
                        (muted, fgHover, transparent),
                        (muted, fg, transparent),
                    ],
                    "Link" =>
                    [
                        (transparent, fg, transparent),
                        (transparent, Get(rule.LinkHover), transparent),
                        (transparent, Get(rule.LinkPressed), transparent),
                        (transparent, Get("FgDisabled"), transparent),
                    ],
                    _ => null,
                };
                if (looks is null)
                {
                    _errors.Add(
                        $"the token compiler has no palette rule for ComponentVariant.{variant}"
                    );
                    continue;
                }

                foreach (var state in states)
                {
                    var (background, foreground, outline) = state switch
                    {
                        "Hover" => looks[1],
                        "Pressed" => looks[2],
                        "Disabled" when looks.Length > 3 => looks[3],
                        _ => looks[0],
                    };
                    theme.Palettes.Add(
                        new PaletteEntry(scheme, variant, state, background, foreground, outline)
                    );
                }
            }
        }

        foreach (var variant in Members("FieldVariant"))
        {
            var flush = variant == "Flush";
            if (!flush && variant != "Default")
            {
                _errors.Add($"the token compiler has no palette rule for FieldVariant.{variant}");
                continue;
            }

            var well = flush ? Get("Transparent") : Get("BgSunken");
            foreach (var state in states)
            {
                var foreground = state == "Disabled" ? Get("FgDisabled") : Get("Fg");
                var border = state switch
                {
                    "Disabled" => Get("BorderDisabled"),
                    _ when flush => Get("Border"),
                    "Hover" => Get("BorderHover"),
                    "Focused" => Get("BorderFocus"),
                    _ => Get("Border"),
                };
                theme.Fields.Add(new PaletteEntry("", variant, state, well, foreground, border));
            }
        }
    }

    void ReadTypography(ThemeData theme, Dictionary<string, Token> flat)
    {
        var where = $" in the {theme.Context} theme";
        foreach (var (member, path) in Family(flat, "FontToken", "font.family.", where))
        {
            if (
                path is not null
                && Resolve(flat, path, TokenKind.FontFamily, out _) is { Family: { } family }
            )
                theme.Fonts.Add((member, family));
        }
        foreach (var (member, path) in Family(flat, "WeightToken", "font.weight.", where))
        {
            var weight =
                path is null ? NormalWeight
                : Resolve(flat, path, TokenKind.FontWeight, out _) is { } read ? read.Number
                : NormalWeight;
            theme.Weights.Add((member, (int)weight));
        }
    }

    void ReadMotion(ThemeData theme, Dictionary<string, Token> flat)
    {
        foreach (var path in flat.Keys)
        {
            if (
                path.StartsWith("motion.", StringComparison.Ordinal)
                && !path.StartsWith("motion.duration.", StringComparison.Ordinal)
                && !path.StartsWith("motion.easing.", StringComparison.Ordinal)
            )
                _errors.Add($"'{path}' is neither a motion.duration nor a motion.easing");
        }

        var where = $" in the {theme.Context} theme";
        foreach (var (member, path) in Family(flat, "DurationToken", "motion.duration.", where))
            theme.Durations.Add(
                (
                    member,
                    path is not null && Resolve(flat, path, TokenKind.Duration, out _) is { } read
                        ? read.Number
                        : 0
                )
            );
        foreach (var (member, path) in Family(flat, "EasingToken", "motion.easing.", where))
            theme.Easings.Add(
                (
                    member,
                    path is not null
                    && Resolve(flat, path, TokenKind.CubicBezier, out _) is { Bezier: { } curve }
                        ? curve
                        : Linear
                )
            );
    }

    DensityData ReadDimensions(string theme, string density, Dictionary<string, Token> flat)
    {
        var data = new DensityData(density);
        var where = $" under {density} in the {theme} theme";
        double Number(string path) => Resolve(flat, path, TokenKind.Dimension, out _)?.Number ?? 0;

        double Required(string path, string slot)
        {
            if (flat.ContainsKey(path))
                return Number(path);
            _warnings.Add($"{slot} has no '{path}'{where}, so it is 0");
            return 0;
        }

        var sizes = Members("ComponentSize");
        foreach (var path in flat.Keys)
        {
            var parts = path.Split('.');
            var known = parts[0] switch
            {
                "size" => parts.Length == 3
                    && sizes.Contains(Pascal(parts[1]))
                    && SizeLeaves.Contains(parts[2]),
                "text" => parts.Length == 3
                    && Members("TextVariant").Contains(Pascal(parts[1]))
                    && parts[2] == "font-size",
                "spacing" => path == "spacing.base",
                _ => true,
            };
            if (!known)
                _errors.Add($"'{path}' is a dimension Tralys.Ui has no slot for{where}");
        }

        foreach (var size in sizes)
        {
            var leaves = new List<(string Leaf, double Value)>(SizeLeaves.Length);
            foreach (var leaf in SizeLeaves)
                leaves.Add((leaf, Required($"size.{Kebab(size)}.{leaf}", $"ComponentSize.{size}")));
            data.Sizes.Add((size, leaves));
        }

        foreach (var variant in Members("TextVariant"))
            data.FontSizes.Add(
                (variant, Required($"text.{Kebab(variant)}.font-size", $"TextVariant.{variant}"))
            );

        foreach (var (member, path) in Family(flat, "RadiusToken", "radius.", where))
            data.Radii.Add((member, path is null ? 0 : Number(path)));
        foreach (var (member, path) in Family(flat, "GripToken", "grip.", where))
            data.Grips.Add((member, path is null ? 0 : Number(path)));

        data.Spacing = Required("spacing.base", "Spacing");
        return data;
    }

    List<(string Member, string? Path)> Family(
        Dictionary<string, Token> flat,
        string vocabulary,
        string prefix,
        string where
    )
    {
        var members = Members(vocabulary);
        foreach (var path in flat.Keys)
        {
            if (
                path.StartsWith(prefix, StringComparison.Ordinal)
                && !members.Contains(Pascal(path.Substring(prefix.Length)))
            )
                _errors.Add($"'{path}' names no member of Tralys.Ui's {vocabulary}");
        }

        var pairs = new List<(string, string?)>();
        foreach (var member in members)
        {
            var path = prefix + Kebab(member);
            if (flat.ContainsKey(path))
                pairs.Add((member, path));
            else
            {
                _warnings.Add($"{vocabulary}.{member} has no '{path}'{where}");
                pairs.Add((member, null));
            }
        }
        return pairs;
    }

    List<string> Members(string vocabulary) => _vocabulary[vocabulary];

    static bool IsDimension(string path)
    {
        foreach (var family in DimensionFamilies)
        {
            if (path.StartsWith(family, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static string SlotName(string path)
    {
        var name = path.Substring(path.IndexOf('.') + 1);
        if (name.EndsWith(".default", StringComparison.Ordinal))
            name = name.Substring(0, name.Length - ".default".Length);
        return Pascal(name);
    }

    public static string Pascal(string path)
    {
        var text = new System.Text.StringBuilder();
        foreach (var segment in path.Split('.', '-'))
        {
            if (segment.Length == 0)
                continue;
            text.Append(char.ToUpperInvariant(segment[0]));
            text.Append(segment, 1, segment.Length - 1);
        }
        return text.ToString();
    }

    static string Kebab(string member)
    {
        var text = new System.Text.StringBuilder();
        for (var i = 0; i < member.Length; i++)
        {
            if (char.IsUpper(member[i]) && i > 0)
                text.Append('-');
            text.Append(char.ToLowerInvariant(member[i]));
        }
        return text.ToString();
    }

    static string Lower(TokenKind kind) =>
        kind switch
        {
            TokenKind.Color => "colour",
            TokenKind.CubicBezier => "cubic bezier",
            TokenKind.FontFamily => "font family",
            TokenKind.FontWeight => "font weight",
            _ => kind.ToString().ToLowerInvariant(),
        };

    // A null hover or pressed fill lays the neutral state layer over the resting fill instead.
    static readonly SchemeRule[] Schemes =
    [
        new(
            "Primary",
            "BgAccent",
            "BgAccentHover",
            "BgAccentPressed",
            "FgAccent",
            "FgAccentHover",
            "BorderAccent",
            "BorderAccentHover",
            "FgFilledAccent",
            false,
            "BgAccentMuted",
            "FgAccentHover",
            "FgAccentHover"
        ),
        new(
            "Secondary",
            "BgRaised",
            null,
            null,
            "Fg",
            "FgHover",
            "Border",
            "BorderHover",
            "Fg",
            true,
            "BgMuted",
            "FgAccent",
            "FgAccentHover"
        ),
        new(
            "Danger",
            "BgDanger",
            "BgDangerHover",
            "BgDangerPressed",
            "FgDanger",
            "FgDanger",
            "BorderDanger",
            "BorderDangerHover",
            "FgFilledDanger",
            true,
            "BgDangerMuted",
            "FgDanger",
            "FgDanger"
        ),
        new(
            "Success",
            "BgSuccess",
            "BgSuccessHover",
            "BgSuccessPressed",
            "FgSuccess",
            "FgSuccess",
            "BorderSuccess",
            "BorderSuccessHover",
            "FgFilledSuccess",
            true,
            "BgSuccessMuted",
            "FgSuccess",
            "FgSuccess"
        ),
    ];

    readonly record struct SchemeRule(
        string Name,
        string Bg,
        string? BgHover,
        string? BgPressed,
        string Fg,
        string FgHover,
        string Border,
        string BorderHover,
        string FilledFg,
        bool FilledBorder,
        string BgMuted,
        string LinkHover,
        string LinkPressed
    );
}
