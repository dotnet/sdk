// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.Add;

public class PackageAddCommandDefinition() : PackageAddCommandDefinitionBase(Name)
{
    public new const string Name = "add";

    public override Argument<string>? GetProjectOrFileArgument()
        => null;
}

public abstract class PackageAddCommandDefinitionBase : Command
{
    public static Option<string> CreateVersionOption() => new Option<string>("--version", "-v")
    {
        Description = CliCommandStrings.CmdVersionDescription,
        HelpName = CliCommandStrings.CmdVersion,
        IsDynamic = true
    }.ForwardAsSingle(o => $"--version {o}");

    public static Option<bool> CreatePrereleaseOption() => new Option<bool>("--prerelease")
    {
        Description = CliStrings.CommandPrereleaseOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--prerelease");
        
    public static Option<string> CreateFrameworkOption() => new Option<string>("--framework", "-f")
    {
        Description = CliCommandStrings.PackageAddCmdFrameworkDescription,
        HelpName = CliCommandStrings.PackageAddCmdFramework
    }.ForwardAsSingle(o => $"--framework {o}");

    public static Option<bool> CreateNoRestoreOption() => new("--no-restore", "-n")
    {
        Description = CliCommandStrings.PackageAddCmdNoRestoreDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<string> CreateSourceOption() => new Option<string>("--source", "-s")
    {
        Description = CliCommandStrings.PackageAddCmdSourceDescription,
        HelpName = CliCommandStrings.PackageAddCmdSource
    }.ForwardAsSingle(o => $"--source {o}");

    public static Option<string> CreatePackageDirOption() => new Option<string>("--package-directory")
    {
        Description = CliCommandStrings.CmdPackageDirectoryDescription,
        HelpName = CliCommandStrings.CmdPackageDirectory
    }.ForwardAsSingle(o => $"--package-directory {o}");

    public static Option<bool> CreateInteractiveOption() => CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    public readonly Argument<PackageIdentityWithRange> PackageIdArgument = CommonArguments.CreateRequiredPackageIdentityArgument();
    public readonly Option<string> VersionOption = CreateVersionOption();
    public readonly Option<bool> PrereleaseOption = CreatePrereleaseOption();
    public readonly Option<string> FrameworkOption = CreateFrameworkOption();
    public readonly Option<bool> NoRestoreOption = CreateNoRestoreOption();
    public readonly Option<string> SourceOption = CreateSourceOption();
    public readonly Option<string> PackageDirOption = CreatePackageDirOption();
    public readonly Option<bool> InteractiveOption = CreateInteractiveOption();
    public readonly Option<string?> ProjectOption = PackageCommandDefinition.CreateProjectOption();
    public readonly Option<string?> FileOption = PackageCommandDefinition.CreateFileOption();

    public PackageAddCommandDefinitionBase(string name)
        : base(name, CliCommandStrings.PackageAddAppFullName)
    {
        VersionOption.Validators.Add(result =>
        {
            if (result.Parent?.GetValue(PackageIdArgument).HasVersion == true)
            {
                result.AddError(CliCommandStrings.ValidationFailedDuplicateVersion);
            }
        });

        Arguments.Add(PackageIdArgument);

        Options.Add(VersionOption);
        Options.Add(FrameworkOption);
        Options.Add(NoRestoreOption);
        Options.Add(SourceOption);
        Options.Add(PackageDirOption);
        Options.Add(InteractiveOption);
        Options.Add(PrereleaseOption);
        Options.Add(ProjectOption);
        Options.Add(FileOption);
    }

    public abstract Argument<string>? GetProjectOrFileArgument();
}
