// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch;

internal static partial class BuildUtilities
{
    private static readonly Regex s_buildDiagnosticRegex = GetBuildDiagnosticRegex();

    [GeneratedRegex(@"[^:]+: (error|warning) [A-Za-z]+[0-9]+: .+")]
    private static partial Regex GetBuildDiagnosticRegex();

    public static void ReportBuildOutput(IReporter reporter, IEnumerable<OutputLine> buildOutput, bool verboseOutput)
    {
        const string BuildEmoji = "🔨";

        foreach (var (line, isError) in buildOutput)
        {
            if (isError)
            {
                reporter.Error(line);
            }
            else if (s_buildDiagnosticRegex.Match(line) is { Success: true } match)
            {
                if (match.Groups[1].Value == "error")
                {
                    reporter.Error(line);
                }
                else
                {
                    reporter.Warn(line);
                }
            }
            else if (verboseOutput)
            {
                reporter.Verbose(line, BuildEmoji);
            }
            else
            {
                reporter.Output(line, BuildEmoji);
            }
        }
    }
}
