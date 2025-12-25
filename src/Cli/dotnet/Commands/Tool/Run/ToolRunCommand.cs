// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run;

internal class ToolRunCommand(
    ParseResult result,
    LocalToolsCommandResolver? localToolsCommandResolver = null,
    ToolManifestFinder? toolManifest = null) : CommandBase(result)
{
    private readonly string? _toolCommandName = result.GetValue(ToolRunCommandParser.CommandNameArgument);
    private readonly IEnumerable<string>? _forwardArgument = result.GetValue(ToolRunCommandParser.CommandArgument);

    private readonly LocalToolsCommandResolver _localToolsCommandResolver = localToolsCommandResolver ?? new LocalToolsCommandResolver(toolManifest);
    public bool _allowRollForward = result.GetValue(ToolRunCommandParser.RollForwardOption);

    public override int Execute()
    {
        return ExecuteCommand(_localToolsCommandResolver, _toolCommandName, _forwardArgument, _allowRollForward);
    }

    public static int ExecuteCommand(LocalToolsCommandResolver commandResolver, string? toolCommandName, IEnumerable<string>? argumentsToForward, bool allowRollForward)
    {
        using var _ = Activities.Source.StartActivity("execute-local-tool");
        CommandSpec commandSpec = commandResolver.ResolveStrict(new CommandResolverArguments()
        {
            // since LocalToolsCommandResolver is a resolver, and all resolver input have dotnet-
            CommandName = $"dotnet-{toolCommandName}",
            CommandArguments = argumentsToForward,
        }, allowRollForward);

        if (commandSpec == null)
        {
            throw new GracefulException([string.Format(CliCommandStrings.CannotFindCommandName, toolCommandName)], isUserError: false);
        }

        var result = CommandFactoryUsingResolver.Create(commandSpec).Execute();
        return result.ExitCode;
    }
}
