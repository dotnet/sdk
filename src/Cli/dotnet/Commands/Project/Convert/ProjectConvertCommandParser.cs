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

    public static readonly Option<bool> KeepSourceOption = new("--keep-source")
    {
        Description = CliCommandStrings.CmdOptionKeepSourceDescription,
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command GetCommand()
    {
        Command command = new("convert", CliCommandStrings.ProjectConvertAppFullName)
        {
            FileArgument,
            SharedOptions.OutputOption,
            KeepSourceOption,
            ForceOption,
            CommonOptions.InteractiveOption(),
        };

        command.SetAction((parseResult) => new ProjectConvertCommand(parseResult).Execute());
        return command;
    }
}
