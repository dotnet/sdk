// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli.CommandLine;

public static class SpanParserExtensions
{
    extension<T>(Option<T> o) where T : ISpanParsable<T>
    {
        /// <summary>
        /// Configures the option with a custom parser that uses the <see cref="ISpanParsable{T}"/> implementation to parse the tokens provided.
        /// Will parse a single token with <see cref="ISpanParser{T}.Parse(ReadOnlySpan{char})"/>, and if the option allows multiple tokens will take the 'last one wins' approach.
        /// </summary>
        /// <remarks>
        /// Without this, Options will fall-back to using potentially-reflection-based parsing in S.CL, or
        /// if the type doesn't have built-in S.CL parsing support will fail to parse at runtime.
        /// </remarks>
        public Option<T> AsSpanParsable()
        {
            o.CustomParser = StaticSingleItemParser<T>;
            return o;
        }
    }

    extension<T>(Option<IReadOnlyCollection<T>> o) where T : ISpanParsable<T>
    {
        /// <summary>
        /// Configures the option with a custom parser that uses the <see cref="ISpanParsable{T}"/> implementation to parse the tokens provided.
        /// This parser handles multiple tokens, using <see cref="ISpanParser{T}.Parse(ReadOnlySpan{char})"/> for each token.
        /// </summary>
        /// <remarks>
        /// Without this, Options will fall-back to using potentially-reflection-based parsing in S.CL, or
        /// if the type doesn't have built-in S.CL parsing support will fail to parse at runtime.
        /// </remarks>
        public Option<IReadOnlyCollection<T>> AsSpanParsable()
        {
            o.CustomParser = StaticMultiItemItemParser<T>;
            return o;
        }
    }

    extension<T>(Argument<T> a) where T : ISpanParsable<T>
    {
        /// <summary>
        /// Configures the argument with a custom parser that uses the <see cref="ISpanParsable{T}"/> implementation to parse the value.
        /// Will parse a single token with <see cref="ISpanParser{T}.Parse(ReadOnlySpan{char})"/>, and if the argument allows multiple tokens will take the 'last one wins' approach.
        /// </summary>
        /// <remarks>
        /// Without this, Arguments will fall-back to using potentially-reflection-based parsing in S.CL, or
        /// if the type doesn't have built-in S.CL parsing support will fail to parse at runtime.
        /// </remarks>
        public Argument<T> AsSpanParsable()
        {
            a.CustomParser = StaticSingleItemParser<T>;
            return a;
        }
    }

    extension<T>(Argument<IReadOnlyCollection<T>> a) where T : ISpanParsable<T>
    {
        /// <summary>
        /// Configures the argument with a custom parser that uses the <see cref="ISpanParsable{T}"/> implementation to parse the value.
        /// This parser handles multiple tokens, using <see cref="ISpanParser{T}.Parse(ReadOnlySpan{char})"/> for each token.
        /// </summary>
        /// <remarks>
        /// Without this, Arguments will fall-back to using potentially-reflection-based parsing in S.CL, or
        /// if the type doesn't have built-in S.CL parsing support will fail to parse at runtime.
        /// </remarks>
        public Argument<IReadOnlyCollection<T>> AsSpanParsable()
        {
            a.CustomParser = StaticMultiItemItemParser<T>;
            return a;
        }
    }

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
