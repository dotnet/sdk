// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.Add;

public static class PackageAddCommandDefinition
{
    public const string Name = "add";
    public const string VersionOptionName = "--version";

    public static Option<string> CreateVersionOption() => new Option<string>(VersionOptionName, "-v")
    {
        Description = CliCommandStrings.CmdVersionDescription,
        HelpName = CliCommandStrings.CmdVersion,
        IsDynamic = true
    }.ForwardAsSingle(o => $"--version {o}");

    public static readonly Option<bool> PrereleaseOption = new Option<bool>("--prerelease")
    {
        Description = CliStrings.CommandPrereleaseOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--prerelease");
        
    public static readonly Option<string> FrameworkOption = new Option<string>("--framework", "-f")
    {
        Description = CliCommandStrings.PackageAddCmdFrameworkDescription,
        HelpName = CliCommandStrings.PackageAddCmdFramework
    }.ForwardAsSingle(o => $"--framework {o}");

    public static readonly Option<bool> NoRestoreOption = new("--no-restore", "-n")
    {
        Description = CliCommandStrings.PackageAddCmdNoRestoreDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> SourceOption = new Option<string>("--source", "-s")
    {
        Description = CliCommandStrings.PackageAddCmdSourceDescription,
        HelpName = CliCommandStrings.PackageAddCmdSource
    }.ForwardAsSingle(o => $"--source {o}");

    public static readonly Option<string> PackageDirOption = new Option<string>("--package-directory")
    {
        Description = CliCommandStrings.CmdPackageDirectoryDescription,
        HelpName = CliCommandStrings.CmdPackageDirectory
    }.ForwardAsSingle(o => $"--package-directory {o}");

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.PackageAddAppFullName);
        AddOptionsAndArguments(command);
        return command;
    }

    public static void AddOptionsAndArguments(Command command)
    {
        var versionOption = CreateVersionOption();
        var packageIdArgument = CommonArguments.CreateRequiredPackageIdentityArgument();

        versionOption.Validators.Add(result =>
        {
            if (result.Parent?.GetValue(packageIdArgument).HasVersion == true)
            {
                result.AddError(CliCommandStrings.ValidationFailedDuplicateVersion);
            }
        });

        command.Arguments.Add(packageIdArgument);

        command.Options.Add(versionOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(SourceOption);
        command.Options.Add(PackageDirOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(PrereleaseOption);
        command.Options.Add(PackageCommandDefinition.ProjectOption);
        command.Options.Add(PackageCommandDefinition.FileOption);
    }
}
