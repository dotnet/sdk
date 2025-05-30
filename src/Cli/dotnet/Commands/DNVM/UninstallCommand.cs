// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.DNVM;
using Semver;

namespace Microsoft.DotNet.Cli.Commands.DNVM;

public class UninstallCommand
{
    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();
        parseResult.ShowHelpOrErrorIfAppropriate();

        return RunAsync(parseResult).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(ParseResult parseResult)
    {
        var sdkVersion = parseResult.GetValue(UninstallCommandParser.SdkVersionArgument);
        var sdkDirString = parseResult.GetValueForOption(UninstallCommandParser.SdkDirOption);

        var env = DnvmEnv.Create();
        var logger = new Logger(LogLevel.Standard);

        // Convert sdk-dir option from string to SdkDirName if provided
        SdkDirName? sdkDir = null;
        if (!string.IsNullOrEmpty(sdkDirString))
        {
            sdkDir = new SdkDirName(sdkDirString);
        }

        return await Microsoft.DotNet.DNVM.UninstallCommand.Run(env, logger, sdkVersion, sdkDir);
    }
}
