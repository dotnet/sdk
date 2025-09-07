using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// An option whose generic type parameter implements <see cref="ISpanParsable{T}"/>, allowing parsing from a single token with no additional customization
/// </summary>
public class SpanParseableOption<T> : Option<T> where T : ISpanParsable<T>
{
    public SpanParseableOption(string name, params string[] aliases) : base(name, aliases)
    {
        // because we know how to parse the T, we can create the custom parser easily
        CustomParser = StaticSingleItemParser;
    }

    internal static T? StaticSingleItemParser(ArgumentResult tokenizationResult)
    {
        if (tokenizationResult.Tokens.Count == 0)
        {
            return default;
        }

        var parentOption = (tokenizationResult.Parent as OptionResult)!.Option;
        // we explicitly only support parsing one token, so let's do a last-one-wins approach here
        var tokenToParse =
            tokenizationResult.Tokens switch
            {
                [var onlyToken] => onlyToken.Value,
                _ => tokenizationResult.Tokens[^1].Value
            };

        if (string.IsNullOrEmpty(tokenToParse))
        {
            tokenizationResult.AddError($"Cannot parse null or empty value for option '{parentOption.Name}'");
        }

        if (!T.TryParse(tokenToParse, null, out var result))
        {
            tokenizationResult.AddError($"Cannot parse value '{tokenToParse}' for option '{parentOption.Name}' as a {typeof(T).Name}");
        }

        return result;
    }
}

/// <summary>
/// An option that contains a collection of <see cref="ISpanParsable{T}"/> items.
/// </summary>
public class SpanParsableCollectionOption<TElem> : Option<IReadOnlyCollection<TElem>>
    where TElem : ISpanParsable<TElem>
{
    public SpanParsableCollectionOption(string name, params string[] aliases) : base(name, aliases)
    {
        // because we know how to parse the T, we can create the custom parser easily
        CustomParser = StaticMultiItemItemParser;
    }

    internal static IReadOnlyCollection<TElem>? StaticMultiItemItemParser(ArgumentResult tokenizationResult)
    {
        if (tokenizationResult.Tokens.Count == 0)
        {
            return default;
        }

        var parentOption = (tokenizationResult.Parent as OptionResult)!.Option;
        var coll = ImmutableArray.CreateBuilder<TElem>(tokenizationResult.Tokens.Count);

        foreach (var token in tokenizationResult.Tokens)
        {
            var tokenToParse = token.Value;

            if (string.IsNullOrEmpty(tokenToParse))
            {
                tokenizationResult.AddError($"Cannot parse null or empty value for option '{parentOption.Name}'");
                continue;
            }

            if (!TElem.TryParse(tokenToParse, null, out var result))
            {
                tokenizationResult.AddError($"Cannot parse value '{tokenToParse}' for option '{parentOption.Name}' as a {typeof(TElem).Name}");
                continue;
            }

            coll.Add(result);
        }

        return coll.ToImmutableArray();
    }
}
