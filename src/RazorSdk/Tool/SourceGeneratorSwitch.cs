// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.NET.Sdk.Razor.Tool;

/// <summary>
/// Opt-in gate for the source-generator-hosted implementation of the <c>generate</c> and
/// <c>discover</c> commands. While the implementation is being validated against the existing
/// direct-engine path, the source generator is used only when explicitly enabled via the
/// <c>RAZOR_TOOL_USE_SOURCE_GENERATOR</c> environment variable.
/// </summary>
internal static class SourceGeneratorSwitch
{
    public const string EnvironmentVariableName = "RAZOR_TOOL_USE_SOURCE_GENERATOR";

    public static bool UseSourceGenerator { get; } = ReadSwitch();

    private static bool ReadSwitch()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
