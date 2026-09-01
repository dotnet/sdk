// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine.Invocation;
using System.Diagnostics;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli;

public class DotNetCommandFactory(bool alwaysRunOutOfProc = false, string currentWorkingDirectory = null) : ICommandFactory
{
    private readonly bool _alwaysRunOutOfProc = alwaysRunOutOfProc;
    private readonly string _currentWorkingDirectory = currentWorkingDirectory;

    public ICommand Create(
        string commandName,
        IEnumerable<string> args,
        NuGetFramework framework = null,
        string configuration = Constants.DefaultConfiguration)
    {
        if (!_alwaysRunOutOfProc && TryGetBuiltInCommand(commandName, out var builtInCommand))
        {
            Debug.Assert(framework == null, "BuiltInCommand doesn't support the 'framework' argument.");
            Debug.Assert(configuration == Constants.DefaultConfiguration, "BuiltInCommand doesn't support the 'configuration' argument.");

            return new BuiltInCommand(commandName, args, builtInCommand);
        }

        return CommandFactoryUsingResolver.CreateDotNet(commandName, args, framework, configuration, _currentWorkingDirectory);
    }

    private static bool TryGetBuiltInCommand(string commandName, out Func<string[], int> commandFunc)
    {
        var command = Parser.GetBuiltInCommand(commandName);
        if (command?.Action is AsynchronousCommandLineAction action)
        {
            commandFunc = (args) => Parser.Invoke([commandName, .. args]);
            return true;
        }
        commandFunc = null;
        return false;
    }
}
