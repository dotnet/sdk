// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Net.BuildServerUtils;

namespace Microsoft.DotNet.Cli.BuildServer;

internal sealed class UnifiedBuildServer : IBuildServer
{
    public int ProcessId => 0; // Not used

    public string Name => CliCommandStrings.UnifiedBuildServer;

    public Task ShutdownAsync()
    {
        var hostServerPath = MSBuildForwardingAppWithoutLogging.GetHostServerPath(createDirectory: false);
        var pipeFolder = BuildServerUtility.GetPipeFolder(hostServerPath);
        Debug.Assert(pipeFolder != null);
        Reporter.Output.WriteLine(CliCommandStrings.ShuttingDownUnifiedBuildServers, pipeFolder);

        return Task.WhenAll(EnumeratePipes(pipeFolder).Select(async file =>
        {
            try
            {
                if (!BuildServerUtility.TryParsePipePath(file, out int pid, out ReadOnlySpan<char> label))
                {
                    throw new InvalidOperationException(string.Format(CliCommandStrings.NamedPipeFileBadFormat, file));
                }

                Reporter.Output.WriteLine(CliCommandStrings.ShuttingDownServerWithPid, label.ToString(), pid);

                // Connect to each pipe.
                var client = new NamedPipeClientStream(BuildServerUtility.NormalizePipeNameForStream(file));
                await using var _ = client.ConfigureAwait(false);
                await client.ConnectAsync().ConfigureAwait(false);

                // Send any data to request shutdown.
                byte[] data = [1];
                await client.WriteAsync(data).ConfigureAwait(false);

                // Wait for the process to exit.
                using var process = Process.GetProcessById(pid);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format(CliCommandStrings.NamedPipeShutdownError, file, ex.Message), ex);
            }
        }));

        static IEnumerable<string> EnumeratePipes(string pipeFolder)
        {
            // On Windows, we need to enumerate all pipes and then filter them.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Debug.Assert(pipeFolder.EndsWith('\\'));
                return Directory.EnumerateFiles(BuildServerUtility.WindowsPipePrefix)
                    .Where(path => path.StartsWith(pipeFolder, StringComparison.OrdinalIgnoreCase) &&
                        !path.AsSpan(pipeFolder.Length).ContainsAny('/', '\\'));
            }

            // On Unix, we can directly enumerate the files in the pipe folder.
            try
            {
                return Directory.EnumerateFiles(pipeFolder);
            }
            catch (DirectoryNotFoundException)
            {
                Reporter.Output.WriteLine(CliCommandStrings.NamedPipeFolderNotFound, pipeFolder);
                return [];
            }
        }
    }
}
