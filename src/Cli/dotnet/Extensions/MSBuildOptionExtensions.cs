// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli;

internal static class MSBuildOptionExtensions
{
    public static Option<string> ForwardAsOutputPath(this Option<string> option, string outputPropertyName, bool surroundWithDoubleQuotes = false)
    {
        return option.SetForwardingFunction((string? outputPath) =>
        {
            if (outputPath is null)
            {
                return [];
            }

            string fullPath = CommandDirectoryContext.GetFullPath(outputPath);
            if (!Path.EndsInDirectorySeparator(fullPath))
            {
                fullPath += Path.DirectorySeparatorChar;
            }

            // Escape the literal path before adding any MSBuild command-line quoting.
            string propertyValue = MSBuildPropertyEscaping.Escape(fullPath);
            if (surroundWithDoubleQuotes)
            {
                propertyValue = MSBuildPropertyParser.SurroundWithDoubleQuotes(propertyValue);
            }

            return [
                $"--property:{outputPropertyName}={propertyValue}",
                "--property:_CommandLineDefinedOutputPath=true"
            ];
        });
    }
}
