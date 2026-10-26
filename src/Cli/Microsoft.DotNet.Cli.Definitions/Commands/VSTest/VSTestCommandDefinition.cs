// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.VSTest;

internal sealed class VSTestCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-vstest";

    public const string TestPlatformOptionName = "--Platform";

    public readonly Option<string> TestPlatformOption = new(TestPlatformOptionName);

    public const string TestFrameworkOptionName = "--Framework";

    public readonly Option<string> TestFrameworkOption = new(TestFrameworkOptionName);

    public const string TestLoggerOptionName = "--logger";

    public readonly Option<string[]> TestLoggerOption = new(TestLoggerOptionName);

    public VSTestCommandDefinition()
        : base("vstest")
    {
        TreatUnmatchedTokensAsErrors = false;
        this.DocsLink = Link;

        Options.Add(TestPlatformOption);
        Options.Add(TestFrameworkOption);
        Options.Add(TestLoggerOption);
    }
}
