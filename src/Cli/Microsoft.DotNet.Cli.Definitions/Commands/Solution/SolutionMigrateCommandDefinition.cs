// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Migrate;

public sealed class SolutionMigrateCommandDefinition : Command
{
    public SolutionMigrateCommandDefinition()
        : base("migrate", CommandDefinitionStrings.MigrateAppFullName)
    {
    }

    public SolutionCommandDefinition Parent => (SolutionCommandDefinition)Parents.Single();
}
