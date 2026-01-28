// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommandDefinition : Command
{
    public readonly Argument<string> FileArgument = new("file")
    {
        Description = CliCommandStrings.CmdFileDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public readonly Option<FileInfo> OutputOption = SharedOptionsFactory.CreateOutputOption();

    public readonly Option<bool> ForceOption = new("--force")
    {
        Description = CliCommandStrings.CmdOptionForceDescription,
        Arity = ArgumentArity.Zero,
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

    public readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = CliCommandStrings.ProjectConvertDryRun,
        Arity = ArgumentArity.Zero,
    };

    public ProjectConvertCommandDefinition()
        : base("convert", CliCommandStrings.ProjectConvertAppFullName)
    {
        Arguments.Add(FileArgument);
        Options.Add(OutputOption);
        Options.Add(ForceOption);
        Options.Add(InteractiveOption);
        Options.Add(DryRunOption);
    }
}
