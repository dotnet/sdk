// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

internal class NuGetCommand
{
    public static int Run(string[] args, bool isFileBasedApp = false)
    {
        return Run(args, isFileBasedApp
            ? new InProcessNuGetCommandRunner(NuGetVirtualProjectBuilder.Instance)
            : new NuGetCommandRunner());
    }

    public static int Run(ParseResult parseResult)
    {
        ICommandRunner runner;

        if (parseResult.CommandResult.Command.Name == "why"
            && parseResult.CommandResult.Command.Arguments.FirstOrDefault() is Argument<string> pathArg
            && parseResult.GetValue(pathArg) is { } path
            && VirtualProjectBuilder.IsValidEntryPointPath(path))
        {
            runner = new InProcessNuGetCommandRunner(NuGetVirtualProjectBuilder.Instance);
        }
        else
        {
            runner = new NuGetCommandRunner();
        }

        return Run(parseResult.GetArguments(), runner);
    }

    public static int Run(string[] args, ICommandRunner nugetCommandRunner)
    {
        DebugHelper.HandleDebugSwitch(ref args);

        if (nugetCommandRunner == null)
        {
            throw new ArgumentNullException(nameof(nugetCommandRunner));
        }
        // replace -? with --help for NuGet CLI
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-?")
                args[i] = "--help";
        }
        return nugetCommandRunner.Run(args);
    }

    private class NuGetCommandRunner : ICommandRunner
    {
        public int Run(string[] args)
        {
            var nugetApp = new NuGetForwardingApp(args);
            nugetApp.WithEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH, GetDotnetPath());
            return nugetApp.Execute();
        }
    }

    private class InProcessNuGetCommandRunner(NuGetVirtualProjectBuilder virtualProjectBuilder) : ICommandRunner
    {
        public int Run(string[] args)
        {
            var originalDotNetHostPath = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH);
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH, GetDotnetPath());
            try
            {
                return global::NuGet.CommandLine.XPlat.Program.Run(args, virtualProjectBuilder);
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH, originalDotNetHostPath);
            }
        }
    }

    private static string GetDotnetPath()
    {
        return new Muxer().MuxerPath;
    }
}
