// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run;

internal class ToolRunCommand(
    ParseResult result,
    LocalToolsCommandResolver localToolsCommandResolver = null,
    ToolManifestFinder toolManifest = null) : CommandBase(result)
{
    private readonly string _toolCommandName = result.GetValue(ToolRunCommandParser.CommandNameArgument);
    private readonly LocalToolsCommandResolver _localToolsCommandResolver = localToolsCommandResolver ?? new LocalToolsCommandResolver();
    private readonly IEnumerable<string> _forwardArgument = result.GetValue(ToolRunCommandParser.CommandArgument);
    public bool _allowRollForward = result.GetValue(ToolRunCommandParser.RollForwardOption);
    private readonly ToolManifestFinder _toolManifest = toolManifest ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
    private readonly bool _fromSource = result.GetValue(ToolRunCommandParser.FromSourceOption);

    private readonly ToolInstallLocalInstaller _toolInstaller = new(result);
    private readonly IToolManifestEditor _toolManifestEditor = new ToolManifestEditor();
    private readonly ILocalToolsResolverCache _localToolsResolverCache = new LocalToolsResolverCache();
    public override int Execute()
    {
        CommandSpec commandspec = _localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
        {
            // since LocalToolsCommandResolver is a resolver, and all resolver input have dotnet-
            CommandName = $"dotnet-{_toolCommandName}",
            CommandArguments = _forwardArgument,

        }, _allowRollForward);

        if (commandspec == null && _fromSource)
        {
            commandspec = GetRemoteCommandSpec();
        }

        if (commandspec == null)
        {
            throw new GracefulException([string.Format(CliCommandStrings.CannotFindCommandName, _toolCommandName)], isUserError: false);
        }

        var result = CommandFactoryUsingResolver.Create(commandspec).Execute();
        return result.ExitCode;
    }

    public CommandSpec? GetRemoteCommandSpec()
    {
        FilePath manifestFile = _toolManifest.FindFirst(true);
        PackageId packageId = new(_toolCommandName);

        IToolPackage toolPackage = _toolInstaller.Install(manifestFile, packageId);

        _toolManifestEditor.Add(
            manifestFile,
            toolPackage.Id,
            toolPackage.Version,
            [toolPackage.Command.Name],
            _allowRollForward);

        _localToolsResolverCache.SaveToolPackage(
            toolPackage,
            _toolInstaller.TargetFrameworkToInstall);

        return _localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
        {
            CommandName = $"dotnet-{toolPackage.Command.Name}",
            CommandArguments = _forwardArgument,
        }, _allowRollForward);
    }
}
