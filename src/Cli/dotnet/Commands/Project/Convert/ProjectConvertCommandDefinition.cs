// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommandDefinition
{
    public const string Name = "convert";

    public static readonly Argument<string> FileArgument = new("file")
    {
        Description = CliCommandStrings.CmdFileDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<bool> ForceOption = new("--force")
    {
        Description = CliCommandStrings.CmdOptionForceDescription,
        Arity = ArgumentArity.Zero,
    };

    public static readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = CliCommandStrings.ProjectConvertDryRun,
        Arity = ArgumentArity.Zero,
    };

    public static Command Create()
        => new(Name, CliCommandStrings.ProjectConvertAppFullName)
        {
            FileArgument,
            SharedOptions.OutputOption,
            ForceOption,
            CommonOptions.CreateInteractiveOption(),
            DryRunOption,
        };
}
