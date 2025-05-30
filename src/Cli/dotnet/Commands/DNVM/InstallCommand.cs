// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.DNVM;
using Semver;

namespace Microsoft.DotNet.Cli.Commands.DNVM;

public class InstallCommand
{
    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();
        parseResult.ShowHelpOrErrorIfAppropriate();

        return RunAsync(parseResult).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(ParseResult parseResult)
    {
        // Map command line options to DNVM InstallCommand.Options
        var sdkVersion = parseResult.GetValue(InstallCommandParser.SdkVersionOption);
        var force = parseResult.GetValueForOption(InstallCommandParser.ForceOption);
        var sdkDirString = parseResult.GetValueForOption(InstallCommandParser.SdkDirOption);
        var verbose = parseResult.GetValueForOption(InstallCommandParser.VerboseOption);

        var env = DnvmEnv.Create();
        var logger = new Logger(verbose ? LogLevel.Info : LogLevel.Standard);

        SdkDirName? sdkDir = null;
        if (!string.IsNullOrEmpty(sdkDirString))
        {
            sdkDir = new SdkDirName(sdkDirString);
        }

        var options = new Microsoft.DotNet.DNVM.InstallCommand.Options
        {
            SdkVersion = sdkVersion,
            Force = force,
            SdkDir = sdkDir,
            Verbose = verbose
        };

        var result = await Microsoft.DotNet.DNVM.InstallCommand.Run(env, logger, options);

        return (int)result;
    }
}