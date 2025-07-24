// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Net.BuildServerUtils;

namespace Microsoft.DotNet.Cli.BuildServer;

internal sealed class UnifiedBuildServer : IBuildServer
{
    public int ProcessId => 0; // Not used

    public string Name => CliCommandStrings.UnifiedBuildServer;

    public Task ShutdownAsync()
    {
        var hostServerPath = MSBuildForwardingAppWithoutLogging.GetHostServerPath(createDirectory: false);
        Reporter.Output.WriteLine(CliCommandStrings.ShuttingDownUnifiedBuildServers, hostServerPath);

        return BuildServerUtility.ShutdownServersAsync(
            onProcessShutdownBegin: static (process) =>
            {
                Reporter.Error.WriteLine(string.Format(CliCommandStrings.ShuttingDownServerWithPid, process.ProcessName, process.Id).Red());
            },
            onError: static (error) =>
            {
                Reporter.Error.WriteLine(string.Format(CliCommandStrings.ShutDownFailed, CliCommandStrings.UnifiedBuildServer, error).Red());
            },
            hostServerPath: hostServerPath);
    }
}
