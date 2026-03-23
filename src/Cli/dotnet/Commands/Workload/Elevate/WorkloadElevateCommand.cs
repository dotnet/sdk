// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Workload.Elevate;

internal class WorkloadElevateCommand(ParseResult parseResult)
    : WorkloadCommandBase<WorkloadElevateCommandDefinition>(parseResult)
{
    public override int Execute()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new GracefulException(CliCommandStrings.RequiresWindows, isUserError: false);
        }

#if !DOT_NET_BUILD_FROM_SOURCE
        NetSdkMsiInstallerServer? server = null;

        try
        {
            server = NetSdkMsiInstallerServer.Create(VerifySignatures);
            server.Run();
        }
        catch (Exception e)
        {
            throw new GracefulException(e.Message, isUserError: false);
        }
        finally
        {
            server?.Shutdown();
        }
#endif

        return 0;
    }
}
