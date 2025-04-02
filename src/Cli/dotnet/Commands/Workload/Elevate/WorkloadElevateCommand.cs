// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Elevate.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Commands.Workload.Elevate;

internal class WorkloadElevateCommand(ParseResult parseResult) : WorkloadCommandBase(parseResult)
{
    private NetSdkMsiInstallerServer _server;

    public override int Execute()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                _server = NetSdkMsiInstallerServer.Create(VerifySignatures);
                _server.Run();
            }
            catch (Exception e)
            {
                throw new GracefulException(e.Message, isUserError: false);
            }
            finally
            {
                _server?.Shutdown();
            }
        }
        else
        {
            throw new GracefulException(LocalizableStrings.RequiresWindows, isUserError: false);
        }

        return 0;
    }
}
