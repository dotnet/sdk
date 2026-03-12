// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Install;

namespace Microsoft.DotNet.Cli.Commands.Tool.Execute;

internal static class ToolExecuteCommandParser

{
    public static readonly Argument<PackageIdentityWithRange> PackageIdentityArgument = ToolInstallCommandParser.PackageIdentityArgument;

    public static readonly Argument<IEnumerable<string>> CommandArgument = new("commandArguments")
    {
        Description = CliCommandStrings.ToolRunArgumentsDescription
    };

    public static readonly Option<string> VersionOption = ToolInstallCommandParser.VersionOption;
    public static readonly Option<bool> RollForwardOption = ToolInstallCommandParser.RollForwardOption;
    public static readonly Option<bool> PrereleaseOption = ToolInstallCommandParser.PrereleaseOption;
    public static readonly Option<string> ConfigOption = ToolInstallCommandParser.ConfigOption;
    public static readonly Option<string[]> SourceOption = ToolInstallCommandParser.SourceOption;
    public static readonly Option<string[]> AddSourceOption = ToolInstallCommandParser.AddSourceOption;
    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption();
    public static readonly Option<bool> YesOption = CommonOptions.YesOption;
    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = ToolInstallCommandParser.VerbosityOption;


    public static readonly Command Command = ConstructCommand();
    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("execute", CliCommandStrings.ToolExecuteCommandDescription);

        command.Aliases.Add("exec");

        command.Arguments.Add(PackageIdentityArgument);
        command.Arguments.Add(CommandArgument);

        command.Options.Add(VersionOption);
        command.Options.Add(YesOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(RollForwardOption);
        command.Options.Add(PrereleaseOption);
        command.Options.Add(ConfigOption);
        command.Options.Add(SourceOption);
        command.Options.Add(AddSourceOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.DisableParallelOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.NoCacheOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.NoHttpCacheOption);
        command.Options.Add(VerbosityOption);

        command.SetAction((parseResult) => new ToolExecuteCommand(parseResult).Execute());

        return command;
    }
}
