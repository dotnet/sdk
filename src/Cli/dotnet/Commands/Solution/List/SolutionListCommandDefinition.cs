// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.List;

public static class SolutionListCommandDefinition
{
    public const string Name = "list";

    public static readonly Option<bool> SolutionFolderOption = new("--solution-folders")
    {
        Description = CliCommandStrings.ListSolutionFoldersArgumentDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<SolutionListOutputFormat> SolutionListFormatOption = new("--format")
    {
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => SolutionListOutputFormat.text,
        Description = CliCommandStrings.SolutionListFormatOptionDescription
    };

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.ListAppFullName);
        command.Options.Add(SolutionFolderOption);
        command.Options.Add(SolutionListFormatOption);
        return command;
    }
}
