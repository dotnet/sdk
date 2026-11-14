// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run.Api;

internal sealed class RunApiCommandParser
{
    [RequiresDynamicCode("Uses MSBuild Object Model types, which are not AOT-safe")]
    public static void ConfigureCommand(RunApiCommandDefinition command)
    {
        command.SetAction((parseResult, cancellationToken) => new RunApiCommand(parseResult).Execute(cancellationToken));
    }
}
