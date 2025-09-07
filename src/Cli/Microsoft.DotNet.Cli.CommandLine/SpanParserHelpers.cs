using System.Collections.Immutable;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.CommandLine;

internal static class SpanParserHelpers
{
    internal static IReadOnlyCollection<T>? StaticMultiItemItemParser<T>(ArgumentResult tokenizationResult)
        where T : ISpanParsable<T>
    {
        if (tokenizationResult.Tokens.Count == 0)
        {
            return default;
        }

        var parentName =
            tokenizationResult.Parent switch
            {
                OptionResult optionResult => optionResult.Option.Name,
                ArgumentResult argumentResult => argumentResult.Argument.Name,
                CommandResult or null => tokenizationResult.Argument.Name,
                _ => "<unknown>"
            };
        var coll = ImmutableArray.CreateBuilder<T>(tokenizationResult.Tokens.Count);

        foreach (var token in tokenizationResult.Tokens)
        {
            var tokenToParse = token.Value;

            if (string.IsNullOrEmpty(tokenToParse))
            {
                tokenizationResult.AddError($"Cannot parse null or empty value for symbol '{parentName}'");
                continue;
            }

            if (!T.TryParse(tokenToParse, null, out var result))
            {
                tokenizationResult.AddError($"Cannot parse value '{tokenToParse}' for symbol '{parentName}' as a {typeof(T).Name}");
                continue;
            }

            coll.Add(result);
        }

        return coll.ToImmutableArray();
    }

    internal static T? StaticSingleItemParser<T>(ArgumentResult tokenizationResult)
        where T : ISpanParsable<T>
    {
        if (tokenizationResult.Tokens.Count == 0)
        {
            return default;
        }

        var parentName =
            tokenizationResult.Parent switch
            {
                OptionResult optionResult => optionResult.Option.Name,
                ArgumentResult argumentResult => argumentResult.Argument.Name,
                CommandResult or null => tokenizationResult.Argument.Name,
                _ => "<unknown>"
            };
        // we explicitly only support parsing one token, so let's do a last-one-wins approach here
        var tokenToParse =
            tokenizationResult.Tokens switch
            {
                [var onlyToken] => onlyToken.Value,
                _ => tokenizationResult.Tokens[^1].Value
            };

        if (string.IsNullOrEmpty(tokenToParse))
        {
            tokenizationResult.AddError($"Cannot parse null or empty value for symbol '{parentName}'");
        }

        if (!T.TryParse(tokenToParse, null, out var result))
        {
            tokenizationResult.AddError($"Cannot parse value '{tokenToParse}' for symbol '{parentName}' as a {typeof(T).Name}");
        }

        return result;
    }
}
