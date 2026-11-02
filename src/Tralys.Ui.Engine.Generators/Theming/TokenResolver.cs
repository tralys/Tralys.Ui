using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Theming;

sealed class ResolverModifier
{
    public Dictionary<string, List<string>> Contexts { get; } = new(StringComparer.Ordinal);

    public string? Default { get; set; }
}

sealed class TokenResolver
{
    const string Version = "2025.10";

    readonly List<(bool IsSet, string Name)> _order = [];

    public Dictionary<string, List<string>> Sets { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ResolverModifier> Modifiers { get; } = new(StringComparer.Ordinal);

    public static TokenResolver? Read(string content, out string? error)
    {
        var resolver = new TokenResolver();
        if (!Json.TryParseObject(content, out var root, out var parseError))
        {
            error = parseError;
            return null;
        }

        if (!root.TryGetValue("version", out var version) || version as string != Version)
        {
            error = $"its version must be \"{Version}\"";
            return null;
        }

        if (root.TryGetValue("sets", out var sets) && sets is Dictionary<string, object?> setMap)
        {
            foreach (var pair in setMap)
            {
                if (
                    pair.Value is not Dictionary<string, object?> body
                    || !body.TryGetValue("sources", out var declared)
                )
                {
                    error = $"set '{pair.Key}' declares no sources";
                    return null;
                }

                if (Sources(declared, out error) is not { } sources)
                {
                    error = $"set '{pair.Key}': {error}";
                    return null;
                }
                resolver.Sets[pair.Key] = sources;
            }
        }

        if (
            root.TryGetValue("modifiers", out var modifiers)
            && modifiers is Dictionary<string, object?> modifierMap
        )
        {
            foreach (var pair in modifierMap)
            {
                if (
                    pair.Value is not Dictionary<string, object?> body
                    || !body.TryGetValue("contexts", out var contexts)
                    || contexts is not Dictionary<string, object?> contextMap
                )
                {
                    error = $"modifier '{pair.Key}' declares no contexts";
                    return null;
                }

                var modifier = new ResolverModifier
                {
                    Default = body.TryGetValue("default", out var fallback)
                        ? fallback as string
                        : null,
                };
                foreach (var context in contextMap)
                {
                    if (Sources(context.Value, out error) is not { } sources)
                    {
                        error = $"modifier '{pair.Key}', context '{context.Key}': {error}";
                        return null;
                    }
                    modifier.Contexts[context.Key] = sources;
                }
                resolver.Modifiers[pair.Key] = modifier;
            }
        }

        if (
            !root.TryGetValue("resolutionOrder", out var order)
            || order is not List<object?> entries
        )
        {
            error = "it declares no resolutionOrder";
            return null;
        }

        foreach (var entry in entries)
        {
            if (
                entry is not Dictionary<string, object?> map
                || !map.TryGetValue("$ref", out var pointer)
                || pointer is not string text
            )
            {
                error = "a resolutionOrder entry must be a { \"$ref\": \"#/sets/...\" } object";
                return null;
            }

            if (text.StartsWith("#/sets/", StringComparison.Ordinal))
                resolver._order.Add((true, text.Substring("#/sets/".Length)));
            else if (text.StartsWith("#/modifiers/", StringComparison.Ordinal))
                resolver._order.Add((false, text.Substring("#/modifiers/".Length)));
            else
            {
                error = $"resolutionOrder entry '{text}' names neither a set nor a modifier";
                return null;
            }
        }

        error = null;
        return resolver;
    }

    public List<string>? Order(Dictionary<string, string> inputs, out string? error)
    {
        var files = new List<string>();
        foreach (var (isSet, name) in _order)
        {
            if (isSet)
            {
                if (!Sets.TryGetValue(name, out var sources))
                {
                    error = $"resolutionOrder names the set '{name}', which is not declared";
                    return null;
                }
                files.AddRange(sources);
                continue;
            }

            if (!Modifiers.TryGetValue(name, out var modifier))
            {
                error = $"resolutionOrder names the modifier '{name}', which is not declared";
                return null;
            }

            var context = inputs.TryGetValue(name, out var chosen) ? chosen : modifier.Default;
            if (context is null)
            {
                error = $"the '{name}' modifier has no default";
                return null;
            }
            if (!modifier.Contexts.TryGetValue(context, out var selected))
            {
                error = $"the '{name}' modifier has no '{context}' context";
                return null;
            }
            files.AddRange(selected);
        }

        error = null;
        return files;
    }

    static List<string>? Sources(object? value, out string? error)
    {
        error = null;
        if (value is not List<object?> items)
        {
            error = "a sources array was expected";
            return null;
        }

        var sources = new List<string>();
        foreach (var item in items)
        {
            if (
                item is Dictionary<string, object?> map
                && map.TryGetValue("$ref", out var reference)
                && reference is string path
            )
            {
                sources.Add(path);
                continue;
            }
            error = "only a { \"$ref\": \"file.tokens.json\" } source is supported";
            return null;
        }
        return sources;
    }
}
