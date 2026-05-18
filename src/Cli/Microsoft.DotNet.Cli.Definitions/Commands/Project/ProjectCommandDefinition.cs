// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Project.Convert;

namespace Microsoft.DotNet.Cli.Commands.Project;

internal sealed class ProjectCommandDefinition : Command
{
    public readonly ProjectConvertCommandDefinition ConvertCommand = new();

    public ProjectCommandDefinition()
        : base("project")
    {
        Subcommands.Add(ConvertCommand);
    }
}
