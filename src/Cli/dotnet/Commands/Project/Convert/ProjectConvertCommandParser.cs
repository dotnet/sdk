// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommandParser
{
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

    public static readonly Option<SourceAction?> SourceOption = new("--source")
    {
        Description = CliCommandStrings.CmdOptionKeepSourceDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public enum SourceAction
    {
        copy,
        move,
    }

    public static Command GetCommand()
    {
        Command command = new("convert", CliCommandStrings.ProjectConvertAppFullName)
        {
            FileArgument,
            SharedOptions.OutputOption,
            SourceOption,
            ForceOption,
            CommonOptions.InteractiveOption(),
        };

        command.SetAction((parseResult) => new ProjectConvertCommand(parseResult).Execute());
        return command;
    }
}
