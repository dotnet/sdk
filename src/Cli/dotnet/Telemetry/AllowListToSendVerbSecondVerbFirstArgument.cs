// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry;

internal class AllowListToSendVerbSecondVerbFirstArgument(HashSet<string> topLevelCommandNameAllowList) : IParseResultLogRule
{
    private HashSet<string> TopLevelCommandNameAllowList { get; } = topLevelCommandNameAllowList;

    public List<TelemetryEntryFormat> AllowList(ParseResult parseResult)
    {
        var result = new List<TelemetryEntryFormat>();
        var topLevelCommandNameFromParse = parseResult.RootSubCommandResult();

        if (topLevelCommandNameFromParse != null)
        {
            var secondVerb = parseResult.Tokens.Where(s => s.Type == TokenType.Command).Skip(1).FirstOrDefault()?.Value ?? "";

            if (TopLevelCommandNameAllowList.Contains(topLevelCommandNameFromParse))
            {
                var firstArgument = parseResult.Tokens.FirstOrDefault(t => t.Type.Equals(TokenType.Argument))?.Value ?? "";
                if (secondVerb != null)
                {
                    result.Add(new TelemetryEntryFormat(
                        "sublevelparser/command",
                        new Dictionary<string, string?>
                        {
                            {"verb", topLevelCommandNameFromParse},
                            {"subcommand", secondVerb},
                            {"argument", firstArgument}
                        }));
                }
            }
        }
        return result;
    }
}
