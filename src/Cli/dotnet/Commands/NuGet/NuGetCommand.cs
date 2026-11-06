// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

internal class NuGetCommand
{
    public static int Run(string[] args, CancellationToken cancellationToken, bool isFileBasedApp = false)
    {
#if CLI_AOT
        // The in-process NuGet runner relies on NuGet.CommandLine.XPlat, which isn't AOT-compatible,
        // so AOT always forwards to the out-of-process NuGet CLI.
        return Run(args, new NuGetCommandRunner(), cancellationToken);
#else
        return Run(args, isFileBasedApp
            ? new InProcessNuGetCommandRunner(NuGetVirtualProjectBuilder.Instance)
            : new NuGetCommandRunner(),
            cancellationToken);
#endif
    }

    public static int Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        ICommandRunner runner;

#if CLI_AOT
        runner = new NuGetCommandRunner();
#else
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
#endif

        return Run(parseResult.GetArguments(), runner, cancellationToken);
    }

    public static int Run(string[] args, ICommandRunner nugetCommandRunner, CancellationToken cancellationToken)
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
        return nugetCommandRunner.Run(args, cancellationToken);
    }

    private class NuGetCommandRunner : ICommandRunner
    {
        public int Run(string[] args, CancellationToken cancellationToken)
        {
            var nugetApp = new NuGetForwardingApp(args);
            nugetApp.WithEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH, GetDotnetPath());
            return nugetApp.Execute(cancellationToken);
        }
    }

#if !CLI_AOT
    private class InProcessNuGetCommandRunner(NuGetVirtualProjectBuilder virtualProjectBuilder) : ICommandRunner
    {
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026",
            Justification =
                "This runner is excluded from CLI_AOT builds")]
        public int Run(string[] args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var originalDotNetHostPath = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH);
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH, GetDotnetPath());
            try
            {
                int exitCode = global::NuGet.CommandLine.XPlat.Program.Run(args, virtualProjectBuilder);
                cancellationToken.ThrowIfCancellationRequested();
                return exitCode;
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentVariableNames.DOTNET_HOST_PATH, originalDotNetHostPath);
            }
        }
    }
#endif

    private static string GetDotnetPath()
    {
        return new Muxer().MuxerPath;
    }
}
