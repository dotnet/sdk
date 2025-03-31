// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run;

internal class ToolRunCommand : CommandBase
{
    private readonly string _toolCommandName;
    private readonly LocalToolsCommandResolver _localToolsCommandResolver;
    private readonly IEnumerable<string> _forwardArgument;
    public bool _allowRollForward;
    private readonly ToolManifestFinder _toolManifest;

    public ToolRunCommand(
        ParseResult result,
        LocalToolsCommandResolver localToolsCommandResolver = null,
        ToolManifestFinder toolManifest = null)
        : base(result)
    {
        _toolCommandName = result.GetValue(ToolRunCommandParser.CommandNameArgument);
        _forwardArgument = result.GetValue(ToolRunCommandParser.CommandArgument);
        _localToolsCommandResolver = localToolsCommandResolver ?? new LocalToolsCommandResolver();
        _allowRollForward = result.GetValue(ToolRunCommandParser.RollForwardOption);
        _toolManifest = toolManifest ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
    }

    public override int Execute()
    {
        CommandSpec commandspec = _localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
        {
            // since LocalToolsCommandResolver is a resolver, and all resolver input have dotnet-
            CommandName = $"dotnet-{_toolCommandName}",
            CommandArguments = _forwardArgument,

        }, _allowRollForward);

        if (commandspec == null)
        {
            throw new GracefulException([string.Format(LocalizableStrings.CannotFindCommandName, _toolCommandName)], isUserError: false);
        }

        var result = CommandFactoryUsingResolver.Create(commandspec).Execute();
        return result.ExitCode;
    }
}
