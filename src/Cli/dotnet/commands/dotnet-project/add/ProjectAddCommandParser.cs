// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using Microsoft.DotNet.Tools.Project.Add;
using Microsoft.TemplateEngine.Cli.Commands;
using LocalizableStrings = Microsoft.DotNet.Tools.Project.Add.LocalizableStrings;

namespace Microsoft.DotNet.Cli;

internal sealed class ProjectAddCommandParser
{
    public static readonly CliArgument<string> FileArgument = new("file")
    {
        Description = LocalizableStrings.CmdFileDescription,
        Arity = ArgumentArity.ExactlyOne,
    };

    public static CliCommand GetCommand()
    {
        CliCommand command = new("add", LocalizableStrings.AppFullName)
        {
            FileArgument,
            SharedOptions.OutputOption,
        };

        command.SetAction((parseResult) => new ProjectAddCommand(parseResult).Execute());
        return command;
    }
}
