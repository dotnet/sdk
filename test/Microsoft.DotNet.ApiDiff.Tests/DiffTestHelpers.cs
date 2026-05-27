// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.Tests;

internal static class DiffTestHelpers
{
    public static string NormalizeWhitespaceOnlyLines(string text)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string lineContent = line.EndsWith('\r') ? line[..^1] : line;
            if (lineContent.Length > 0 && string.IsNullOrWhiteSpace(lineContent))
            {
                lines[i] = line.EndsWith('\r') ? "\r" : string.Empty;
            }
        }

        return string.Join('\n', lines);
    }
}
