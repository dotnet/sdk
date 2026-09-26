// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.CodeAnalysis;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace Microsoft.NET.Sdk.Razor.Tool;

/// <summary>
/// Reads the Razor source generator's host output (<see cref="RazorGeneratorResult"/>). That type is
/// internal to the Razor compiler assembly and reached here through its <c>InternalsVisibleTo("rzc")</c>
/// grant, and <see cref="GeneratorRunResult.HostOutputs"/> is an experimental API; both are isolated to
/// this file so the rest of the host code depends only on the public generator surface.
/// </summary>
internal static class RazorSourceGeneratorHostOutput
{
    public static bool TryGet(GeneratorRunResult result, out RazorGeneratorResult razorResult)
    {
#pragma warning disable RSEXPERIMENTAL004 // HostOutputs is for evaluation purposes only.
        if (result.HostOutputs.TryGetValue(nameof(RazorGeneratorResult), out var value) &&
            value is RazorGeneratorResult typedResult)
#pragma warning restore RSEXPERIMENTAL004
        {
            razorResult = typedResult;
            return true;
        }

        razorResult = null;
        return false;
    }
}
