// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal static class PackCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-pack";

    public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new(CliStrings.SolutionOrProjectArgumentName)
    {
        Description = CliStrings.SolutionOrProjectArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = CliCommandStrings.PackCmdOutputDirDescription,
        HelpName = CliCommandStrings.PackCmdOutputDir
    }.ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o)}");

    public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
    {
        Description = CliCommandStrings.CmdNoBuildOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:NoBuild=true");

    public static readonly Option<bool> IncludeSymbolsOption = new ForwardedOption<bool>("--include-symbols")
    {
        Description = CliCommandStrings.CmdIncludeSymbolsDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSymbols=true");

    public static readonly Option<bool> IncludeSourceOption = new ForwardedOption<bool>("--include-source")
    {
        Description = CliCommandStrings.CmdIncludeSourceDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSource=true");

    public static readonly Option<bool> ServiceableOption = new ForwardedOption<bool>("--serviceable", "-s")
    {
        Description = CliCommandStrings.CmdServiceableDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:Serviceable=true");

    public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
    {
        Description = CliCommandStrings.PackCmdNoLogo,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-nologo");

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.PackConfigurationOptionDescription);

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new DocumentedCommand("pack", DocsLink, CliCommandStrings.PackAppFullName);

        command.Arguments.Add(SlnOrProjectArgument);
        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(NoBuildOption);
        command.Options.Add(IncludeSymbolsOption);
        command.Options.Add(IncludeSourceOption);
        command.Options.Add(ServiceableOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(CommonOptions.VersionSuffixOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: true, includeNoDependenciesOption: true);

        command.SetAction(PackCommand.Run);

        return command;
    }
}
