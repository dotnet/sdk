// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommandDefinition : Command
{
    public readonly Argument<string> FileArgument = new("file")
    {
        Description = CommandDefinitionStrings.CmdFileDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public readonly Option<FileInfo> OutputOption = new("--output", "-o")
    {
        Description = CommandDefinitionStrings.Option_Output,
        Required = false,
        Arity = new ArgumentArity(1, 1)
    };

    public readonly Option<bool> ForceOption = new("--force")
    {
        Description = CommandDefinitionStrings.CmdOptionForceDescription,
        Arity = ArgumentArity.Zero,
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

    public readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = CommandDefinitionStrings.ProjectConvertDryRun,
        Arity = ArgumentArity.Zero,
    };

    public ProjectConvertCommandDefinition()
        : base("convert", CommandDefinitionStrings.ProjectConvertAppFullName)
    {
        Arguments.Add(FileArgument);
        Options.Add(OutputOption);
        Options.Add(ForceOption);
        Options.Add(InteractiveOption);
        Options.Add(DryRunOption);
    }
}
