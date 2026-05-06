// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Workloads.Workload.Elevate
{
    internal class WorkloadElevateCommand : WorkloadCommandBase
    {
        private NetSdkMsiInstallerServer _server;

        public WorkloadElevateCommand(ParseResult parseResult) : base(parseResult)
        {
        }

        public override int Execute()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // Capture the unelevated client's temp directory (if supplied) so path validators
                    // can accept IPC-supplied paths that originate from it. Optional and ignored when null
                    // or unparseable; in either case validators fall back to the server's own temp.
                    string clientTemp = _parseResult.GetValue(WorkloadElevateCommandParser.ClientTempOption);
                    if (!string.IsNullOrWhiteSpace(clientTemp))
                    {
                        try
                        {
                            InstallerBase.TrustedClientTempDirectory = System.IO.Path.GetFullPath(clientTemp);
                        }
                        catch
                        {
                            // Ignore malformed values.
                        }
                    }

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
}
