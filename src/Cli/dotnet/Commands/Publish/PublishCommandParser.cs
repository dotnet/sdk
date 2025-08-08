// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Publish;

internal static class PublishCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-publish";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = CliCommandStrings.PublishOutputOptionDescription,
        HelpName = CliCommandStrings.PublishOutputOption
    }.ForwardAsOutputPath("PublishDir");

    public static readonly Option<IEnumerable<string>> ManifestOption = new ForwardedOption<IEnumerable<string>>("--manifest")
    {
        Description = CliCommandStrings.ManifestOptionDescription,
        HelpName = CliCommandStrings.ManifestOption
    }.ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}")
    .AllowSingleArgPerToken();

    public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
    {
        Description = CliCommandStrings.NoBuildOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:NoBuild=true");

    public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
    {
        Description = CliCommandStrings.PublishCmdNoLogo,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-nologo");

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

    public static readonly Option<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

    public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption(CliCommandStrings.PublishRuntimeOptionDescription);

    public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.PublishFrameworkOptionDescription);

    public static readonly Option<string?> ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.PublishConfigurationOptionDescription);
    public static readonly Option<string[]> TargetOption = CommonOptions.RequiredMSBuildTargetOption("Publish", [("_IsPublishing", "true")]);

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = BuildCommandParser.VerbosityOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new DocumentedCommand("publish", DocsLink, CliCommandStrings.PublishAppDescription);

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: true);

        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(ManifestOption);
        command.Options.Add(NoBuildOption);
        command.Options.Add(SelfContainedOption);
        command.Options.Add(NoSelfContainedOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(RuntimeOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(CommonOptions.VersionSuffixOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(CommonOptions.BinaryLoggerOption);
        command.Options.Add(TargetOption);

        command.SetAction(PublishCommand.Run);

        return command;
    }
}
