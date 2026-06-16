// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Helpers for surfacing Azure Pipelines logging commands produced by Microsoft.Testing.Platform
/// test modules (for example by the <c>Microsoft.Testing.Extensions.AzureDevOpsReport</c> extension)
/// when they run under <c>dotnet test</c>.
/// </summary>
/// <remarks>
/// Under <c>dotnet test</c> the SDK launches each test module with its standard output redirected and
/// captured (see <see cref="TestApplication"/>). Azure Pipelines logging commands such as
/// <c>##vso[task.logissue ...]</c> only have an effect when written to the standard output of the
/// pipeline step, which here belongs to the <c>dotnet test</c> process, not the captured child. So the
/// SDK has to recognize these command lines and re-emit them on its own standard output.
/// See <see href="https://learn.microsoft.com/azure/devops/pipelines/scripts/logging-commands">logging commands</see>
/// and <see href="https://github.com/microsoft/testfx/issues/5979"/>.
/// </remarks>
internal static class AzureDevOpsUtilities
{
    // Opt-out: setting TESTINGPLATFORM_AZDO_OUTPUT to one of the "off" values disables the
    // automatic surfacing of Azure Pipelines logging commands even when TF_BUILD=true. This mirrors
    // the opt-out honored by Microsoft.Testing.Platform itself so both sides agree on the behavior.
    private const string OptOutEnvironmentVariableName = "TESTINGPLATFORM_AZDO_OUTPUT";

    /// <summary>
    /// Returns <see langword="true"/> when the current process is running on an Azure DevOps agent
    /// (<c>TF_BUILD=true</c>) and the user has not opted out via
    /// <c>TESTINGPLATFORM_AZDO_OUTPUT=off|false|0</c>.
    /// </summary>
    public static bool IsAzureDevOpsEnvironment()
        => IsAzureDevOpsEnvironment(Environment.GetEnvironmentVariable);

    internal static bool IsAzureDevOpsEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        if (!bool.TryParse(getEnvironmentVariable("TF_BUILD"), out bool tfBuild) || !tfBuild)
        {
            return false;
        }

        string? optOut = getEnvironmentVariable(OptOutEnvironmentVariableName);
        return string.IsNullOrEmpty(optOut) || !IsOffValue(optOut);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the given line is an Azure Pipelines logging command, i.e.
    /// it starts (after optional leading whitespace) with <c>##vso[</c> or <c>##[</c>.
    /// </summary>
    public static bool IsAzureDevOpsLoggingCommand(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        ReadOnlySpan<char> span = line.AsSpan().TrimStart();
        return span.StartsWith("##vso[".AsSpan(), StringComparison.Ordinal)
            || span.StartsWith("##[".AsSpan(), StringComparison.Ordinal);
    }

    private static bool IsOffValue(string value)
        => value.Equals("off", StringComparison.OrdinalIgnoreCase)
            || value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0", StringComparison.Ordinal);
}
