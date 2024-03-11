// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tools;

internal static class EnvironmentVariables
{
    public static readonly bool VerboseCliOutput = ReadBool("DOTNET_CLI_CONTEXT_VERBOSE");
    public static bool SuppressEmojis = ReadBool("DOTNET_WATCH_SUPPRESS_EMOJIS");

    private static bool ReadBool(string variableName)
        => Environment.GetEnvironmentVariable(variableName) is var value && (value == "1" || bool.TryParse(value, out var boolValue) && boolValue);
}
