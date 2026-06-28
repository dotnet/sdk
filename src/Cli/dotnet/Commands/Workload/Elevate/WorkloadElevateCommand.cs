// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Installer.Windows;
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

#if TARGET_WINDOWS
        // Capture the unelevated client's temp directory (if supplied) so path validators
        // can accept IPC-supplied paths that originate from it. Optional and ignored when null
        // or unparseable; in either case validators fall back to the server's own temp.
        // Use the inherited _parseResult field rather than the primary constructor parameter to avoid
        // CS9107 (capturing the parameter into the type's state on top of the base ctor passthrough).
        string? clientTemp = _parseResult.GetValue(Definition.ClientTempOption);
        if (!string.IsNullOrWhiteSpace(clientTemp))
        {
            try
            {
                InstallerBase.TrustedClientTempDirectory = Path.GetFullPath(clientTemp);
            }
            catch
            {
                // Ignore malformed values.
            }
        }

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
