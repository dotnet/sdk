// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

#if WASM_TASKS
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class StaticWebAssetPathPattern : IEquatable<StaticWebAssetPathPattern>
#else
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class StaticWebAssetPathPattern : IEquatable<StaticWebAssetPathPattern>
#endif
{
    public StaticWebAssetPathPattern(string path) => RawPattern = path;

    public StaticWebAssetPathPattern(List<StaticWebAssetPathSegment> segments)
    {
        RawPattern = GetRawPattern(segments);
        Segments = segments;
    }

    public string RawPattern { get; private set; }

    public IList<StaticWebAssetPathSegment> Segments { get; set; } = [];

    // Tokens in static web assets represent a similar concept to tokens within routing. They can be used to identify logical
    // values that need to be replaced by well-known strings. The format for defining a token in static web assets is as follows
    // #[.{tokenName}].
    // # is used to make sure we never interpret any valid file path as a token (since # is not allowed to appear in file systems)
    // [] delimit the token expression.
    // Inside the [] there is a token expression that is represented as an interpolated string where {} delimit the variables and
    // the content inside the name of the value they need to be replaced with.
    // The variables might contain 'embeded' values represented by = after the variable name, for example {tokenName=value} this allows
    // us to preserve the original token information when we define related endpoints that required values from their related assets.
    // The expression inside the `[]` can contain any character that can appear in the file system, for example, to indicate that
    // a fixed prefix needs to be added.
    // An expression can be followed by `?` to indicate that the entire token expression is optional and we don't want to fingerprint
    // the file (this indicates that the asset can logically be referenced with or without the expression.
    // For example file[.{integrity}]?.js will mean, the file can be addressed as file.js (no integrity  suffix) or file.asdfasdf.js where
    // '.asdfasdf' is the integrity suffix.
    // An expression can be followed by `!` to indicate that the entire token expression is optional and that we want to fingerprint the
    // file (this indicates that the asset can logically be referenced with or without the expression, but we want to fingerprint the file) but
    // the file on disk will contain the fingerprint.
    // For example file[.{integrity}]!.js will mean, the file can be addressed as file.js (no integrity  suffix) or file.asdfasdf.js where
    // '.asdfasdf' is the integrity suffix, but the file on disk will be named file.asdfasdf.js.
    // Encoding this logic on the path allows other tasks to make decissions on which route to use based on whether they control the hosting
    // or they require the host to match the file name on disk.
    // The reason we want to plan for this is that we don't have the ability to post process all content from the app (CSS files, JS, etc.)
    // to replace the original paths with the replaced paths. This means some files should be served in their original formats so that they
    // work with the content that we couldn't post process, and with the post processed format, so that they can benefit from fingerprinting
    // and other features. This is why we want to bake into the format itself the information that specifies under which paths the file will
    // be available at runtime so that tasks/tools can operate independently and produce correct results.
    // The current token we support is the 'fingerprint' token, which computes a web friendly version of the hash of the file suitable
    // to be embedded in other contexts.     
    // We might include other tokens in the future, like `[{basepath}]` to give a file the ability to have its path be relative to the consuming
    // project base path, etc.
    public static StaticWebAssetPathPattern Parse(string rawPath, string assetIdentity = null)
    {
        var pattern = new StaticWebAssetPathPattern(rawPath);
        var nextToken = rawPath.IndexOf("#[", StringComparison.OrdinalIgnoreCase);
        if (nextToken == -1)
        {
            var literalSegment = new StaticWebAssetPathSegment();
            literalSegment.Parts.Add(new StaticWebAssetSegmentPart { Name = rawPath, IsLiteral = true });
            pattern.Segments.Add(literalSegment);
            return pattern;
        }

        if (nextToken > 0)
        {
            var literalSegment = new StaticWebAssetPathSegment();
            literalSegment.Parts.Add(new StaticWebAssetSegmentPart { Name = rawPath.Substring(0, nextToken), IsLiteral = true });
            pattern.Segments.Add(literalSegment);
        }
        while (nextToken != -1)
        {
            var tokenEnd = rawPath.IndexOf(']', nextToken);
            if (tokenEnd == -1)
            {
                if (assetIdentity != null)
                {
                    // We don't have a closing token, this is likely an error, so throw
                    throw new InvalidOperationException($"Invalid relative path '{rawPath}' for asset '{assetIdentity}'. Missing ']' token.");
                }
                else
                {
                    throw new InvalidOperationException($"Invalid token expression '{rawPath}'. Missing ']' token.");
                }
            }

            var tokenExpression = rawPath.Substring(nextToken + 2, tokenEnd - nextToken - 2);

            var token = new StaticWebAssetPathSegment();
            AddTokenSegmentParts(tokenExpression, token);
            pattern.Segments.Add(token);

            // Check if the segment is optional (ends with ? or !)
            if (tokenEnd < rawPath.Length - 1 && (rawPath[tokenEnd + 1] == '?' || rawPath[tokenEnd + 1] == '!'))
            {
                token.IsOptional = true;
                if (rawPath[tokenEnd + 1] == '!')
                {
                    token.IsPreferred = true;
                }
                tokenEnd++;
            }

            nextToken = rawPath.IndexOf("#[", tokenEnd, comparisonType: StringComparison.OrdinalIgnoreCase);

            // Add a literal segment if there is more content after the token and before the next one
            if ((nextToken != -1 && nextToken > tokenEnd + 1) || (nextToken == -1 && tokenEnd < rawPath.Length - 1))
            {
                var literalEnd = nextToken == -1 ? rawPath.Length : nextToken;
                var literalSegment = new StaticWebAssetPathSegment();
                literalSegment.Parts.Add(new StaticWebAssetSegmentPart { Name = rawPath.Substring(tokenEnd + 1, literalEnd - tokenEnd - 1), IsLiteral = true });
                pattern.Segments.Add(literalSegment);
            }
        }

        return pattern;
    }

    // Replaces the tokens in the pattern with values provided in the expression, by the asset, or global resolvers.
    // Embedded values allow tasks to define the values that should be used when defining endpoints, while preserving the
    // original token information (for example, if its optional or if it should be preferred).
    // Values provided in the expression take precedence over values provided by the asset or global resolvers.
    // Values provided by the asset take precedence over values provided by the global resolvers.
    // Right now the only available value is the fingerprint value.
    // Global values in the future can include user defined tokens, like versions, etc. (For example, dotnet version, blazor web.js version, etc.)
    // The applyPreferences parameter is used to determine if we should apply the preferences defined in the pattern, for example, if we should
    // skip optional segments that are not preferred.
    // Preferences are applied when we are generating file names for the final asset location on disk, in which case we need to reduce the expression
    // to a single literal path.
#if WASM_TASKS
    internal (string Path, Dictionary<string, string> PatternValues) ReplaceTokens(StaticWebAsset staticWebAsset, StaticWebAssetTokenResolver tokens, bool applyPreferences = false)
#else
    public (string Path, Dictionary<string, string> PatternValues) ReplaceTokens(StaticWebAsset staticWebAsset, StaticWebAssetTokenResolver tokens, bool applyPreferences = false)
#endif
    {
        var result = new StringBuilder();
        var dictionary = new Dictionary<string, string>();
        foreach (var segment in Segments)
        {
            if (IsLiteralSegment(segment))
            {
                result.Append(segment.Parts[0].Name);
            }
            else
            {
                if (applyPreferences && segment.IsOptional && !segment.IsPreferred)
                {
                    continue;
                }

                var tokenNames = segment.GetTokenNames();
                var foundAllValues = true;
                var missingValue = "";
                foreach (var tokenName in tokenNames)
                {
                    if (!tokens.TryGetValue(staticWebAsset, tokenName, out var tokenValue) || string.IsNullOrEmpty(tokenValue))
                    {
                        foundAllValues = false;
                        missingValue = tokenName;
                        break;
                    }

                    dictionary[tokenName] = tokenValue;
                }

                if (!foundAllValues && !segment.IsOptional)
                {
                    // We are missing a value in the expression for a non-optional segment.
                    throw new InvalidOperationException($"Token '{missingValue}' not provided for '{RawPattern}'.");
                }
                else if (!foundAllValues)
                {
                    // Missing a value on an optional expression, this means we don't append this segment.
                    continue;
                }
                else
                {
                    // We have all the values, so we can replace the tokens in the segment.
                    foreach (var part in segment.Parts)
                    {
                        if (part.IsLiteral)
                        {
                            result.Append(part.Name);
                        }
                        else if (!string.IsNullOrEmpty(part.Value))
                        {
                            // Token was embedded, so add it to the dictionary.
                            dictionary[part.Name] = part.Value;
                            result.Append(part.Value);
                        }
                        else
                        {
                            result.Append(dictionary[part.Name]);
                        }
                    }
                }
            }
        }

        return (result.ToString(), dictionary);
    }

    // Extracts more than one pattern from a single pattern expression, creating separate patterns for each possible combination of optional segments.
    // This is what transforms a pattern like 'file[.{fingerprint}]?.js' into two patterns 'file[.{fingerprint}]?.js' and 'file.js', which are then used
    // when we define endpoints.
    // During the build the patterns are not "reduced" into their final form so that we can use the pattern expression through the build to refer to a given
    // endpoint by its pattern expression instead of by its final path.
    public IEnumerable<StaticWebAssetPathPattern> ExpandPatternExpression()
    {
        // We are going to analyze each segment and produce the following:
        // - For literals, we just concatenate 
        // - For parameter expressions without '?' we return the parameter expression.
        // - For parameter expressions with '?' we return
        // For example:
        // - asset.css produces a single pattern (asset.css).
        // - other#[.{fingerprint}].js produces a single pattern asset#[.{fingerprint}].js
        // - last#[.{fingerprint}]?.txt produces two patterns last#[.{fingerprint}]?.txt and last.txt

        var hasOptionalSegments = false;
        foreach (var segment in Segments)
        {
            if (segment.IsOptional)
            {
                hasOptionalSegments = true;
                break;
            }
        }

        if (!hasOptionalSegments)
        {
            return [this];
        }
        List<List<StaticWebAssetPathSegment>> expandedPatternSegments = [];

        for (var i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];
            if (IsLiteralSegment(segment) || !segment.IsOptional)
            {
                if (expandedPatternSegments.Count == 0)
                {
                    expandedPatternSegments.Add([segment]);
                }
                else
                {
                    for (var j = 0; j < expandedPatternSegments.Count; j++)
                    {
                        var expandedPattern = expandedPatternSegments[j];
                        expandedPattern.Add(segment);
                    }
                }
            }
            else
            {
                var count = expandedPatternSegments.Count;
                if (count == 0)
                {
                    expandedPatternSegments.Add([]);
                    expandedPatternSegments.Add([MakeRequiredSegment(segment)]);
                }
                else
                {
                    for (var j = 0; j < count; j++)
                    {
                        var expandedPattern = expandedPatternSegments[j];
                        expandedPatternSegments.Add([.. expandedPattern, MakeRequiredSegment(segment)]);
                    }
                }
            }
        }

        var result = new List<StaticWebAssetPathPattern>();
        foreach (var expandedPattern in expandedPatternSegments)
        {
            result.Add(new StaticWebAssetPathPattern(expandedPattern));
        }

        return result;

        static StaticWebAssetPathSegment MakeRequiredSegment(StaticWebAssetPathSegment segment) => new()
        {
            Parts = segment.Parts,
            IsOptional = false
        };
    }

    // Computes the label for the pattern. The label is the pattern without the token expressions.
    // The label is used as a stable way to identify any pattern that has token expressions in it.
    // The combination of label + values applied to the pattern uniquely identifies the pattern.
    // For example, the pattern 'file[.{fingerprint}]?.js' has a label of 'file.js'.
    // The combination of 'file.js' + {fingerprint=asdfasdf} uniquely identifies the resolved pattern 'file.asdfasdf.js'.
    // This is leveraged at runtime to identify fingerprinted assets, and create a reverse map from the fingerprinted pattern
    // to the original file without fingerprint.
    internal string ComputePatternLabel()
    {
        var result = new StringBuilder();
        foreach (var segment in Segments)
        {
            if (IsLiteralSegment(segment))
            {
                result.Append(segment.Parts[0].Name);
            }
            continue;
        }

        return result.ToString();
    }

    // Embeds the tokens in the pattern with the values provided by the asset or global resolvers.
    // The embedded values allow tasks to define patterns for related assets/endpoints that retain
    // values from the original asset.
    // For example, when defining endpoints for gzip compressed files. If the origianl asset has a
    // fingerprint token in the pattern, we want the fingerprint of the gzip compressed file to be
    // that of the uncompressed file, not the fingerprint of the compressed file.
    internal void EmbedTokens(StaticWebAsset staticWebAsset, StaticWebAssetTokenResolver resolver)
    {
        foreach (var segment in Segments)
        {
            if (IsLiteralSegment(segment))
            {
                continue;
            }
            var tokenNames = segment.GetTokenNames();
            foreach (var tokenName in tokenNames)
            {
                foreach (var part in segment.Parts)
                {
                    if (part.IsLiteral)
                    {
                        continue;
                    }

                    if (!resolver.TryGetValue(staticWebAsset, tokenName, out var tokenValue) || string.IsNullOrEmpty(tokenValue))
                    {
                        continue;
                    }

                    if (string.Equals(part.Name, tokenName))
                    {
                        part.Value = tokenValue;
                    }
                }
            }
        }
        RawPattern = GetRawPattern(Segments);
    }

    // Iterate over the token expression and add the parts to the token segment
    // Some examples are '.{fingerprint}', '{fingerprint}.', '{fingerprint}{fingerprint}', {fingerprint}.{fingerprint}
    // The '.' represents sample literal content.
    // The value within the {} represents token variables.
    private static void AddTokenSegmentParts(string tokenExpression, StaticWebAssetPathSegment token)
    {
        var nextToken = tokenExpression.IndexOf('{');
        if (nextToken is not (-1) and > 0)
        {
            var literalPart = new StaticWebAssetSegmentPart { Name = tokenExpression.Substring(0, nextToken), IsLiteral = true };
            token.Parts.Add(literalPart);
        }
        while (nextToken != -1)
        {
            var tokenEnd = tokenExpression.IndexOf('}', nextToken);
            if (tokenEnd == -1)
            {
                throw new InvalidOperationException($"Invalid token expression '{tokenExpression}'. Missing '}}' token.");
            }

            var embeddedValue = tokenExpression.IndexOf('=', nextToken);
            if (embeddedValue != -1)
            {
                var tokenPart = new StaticWebAssetSegmentPart
                {
                    Name = tokenExpression.Substring(nextToken + 1, embeddedValue - nextToken - 1),
                    IsLiteral = false,
                    Value = tokenExpression.Substring(embeddedValue + 1, tokenEnd - embeddedValue - 1)
                };
                token.Parts.Add(tokenPart);
            }
            else
            {
                var tokenPart = new StaticWebAssetSegmentPart { Name = tokenExpression.Substring(nextToken + 1, tokenEnd - nextToken - 1), IsLiteral = false };
                token.Parts.Add(tokenPart);
            }

            nextToken = tokenExpression.IndexOf('{', tokenEnd);
            if ((nextToken != -1 && nextToken > tokenEnd + 1) || (nextToken == -1 && tokenEnd < tokenExpression.Length - 1))
            {
                var literalEnd = nextToken == -1 ? tokenExpression.Length : nextToken;
                var literalPart = new StaticWebAssetSegmentPart { Name = tokenExpression.Substring(tokenEnd + 1, literalEnd - tokenEnd - 1), IsLiteral = true };
                token.Parts.Add(literalPart);
            }
        }
    }

    private static string GetRawPattern(IList<StaticWebAssetPathSegment> segments)
    {
        var stringBuilder = new StringBuilder();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var isLiteral = IsLiteralSegment(segment);
            if (!isLiteral)
            {
                stringBuilder.Append("#[");
            }
            for (var j = 0; j < segment.Parts.Count; j++)
            {
                var part = segment.Parts[j];
                stringBuilder.Append(part.IsLiteral ? part.Name : $$"""{{{(!string.IsNullOrEmpty(part.Value) ? $"""{part.Name}={part.Value}""" : part.Name)}}}""");
            }
            if (!isLiteral)
            {
                stringBuilder.Append(']');
                if (segment.IsOptional)
                {
                    if (segment.IsPreferred)
                    {
                        stringBuilder.Append('!');
                    }
                    else
                    {
                        stringBuilder.Append('?');
                    }
                }
            }
        }

        return stringBuilder.ToString();
    }

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetPathPattern);

    public bool Equals(StaticWebAssetPathPattern other) => other is not null && RawPattern == other.RawPattern && Segments.SequenceEqual(other.Segments);

#if NET47_OR_GREATER
    public override int GetHashCode()
    {
        var hashCode = 1219904980;
        hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(RawPattern);
        hashCode = (hashCode * -1521134295) + EqualityComparer<IList<StaticWebAssetPathSegment>>.Default.GetHashCode(Segments);
        return hashCode;
    }
#else
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(RawPattern);
        for (var i = 0; i < Segments.Count; i++)
        {
            hashCode.Add(Segments[i]);
        }
        return hashCode.ToHashCode();
    }
#endif

    public static bool operator ==(StaticWebAssetPathPattern left, StaticWebAssetPathPattern right) => EqualityComparer<StaticWebAssetPathPattern>.Default.Equals(left, right);
    public static bool operator !=(StaticWebAssetPathPattern left, StaticWebAssetPathPattern right) => !(left == right);

    private string GetDebuggerDisplay() => string.Concat(Segments.Select(s => s.GetDebuggerDisplay()));

    private static bool IsLiteralSegment(StaticWebAssetPathSegment segment) => segment.Parts.Count == 1 && segment.Parts[0].IsLiteral;
    internal static string PathWithoutTokens(string path)
    {
        return Parse(path).ComputePatternLabel();
    }
}
