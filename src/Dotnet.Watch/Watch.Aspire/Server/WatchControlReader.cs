// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class WatchControlReader : IAsyncDisposable
{
    private readonly ProjectLauncher _launcher;
    private readonly string _pipeName;
    private readonly NamedPipeClientStream _pipe;
    private readonly CancellationTokenSource _disposalCancellationSource = new();
    private readonly Task _listener;

    public WatchControlReader(string pipeName, ProjectLauncher launcher)
    {
        _pipe = new NamedPipeClientStream(
            serverName: ".",
            pipeName,
            PipeDirection.In,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        _pipeName = pipeName;
        _launcher = launcher;
        _listener = ListenAsync(_disposalCancellationSource.Token);
    }

    private ILogger Logger
        => _launcher.Logger;

    public async ValueTask DisposeAsync()
    {
        Logger.LogDebug("Disposing control pipe.");

        _disposalCancellationSource.Cancel();
        await _listener;

        try
        {
            await _pipe.DisposeAsync();
        }
        catch (IOException)
        {
            // Pipe may already be broken if the server disconnected
        }

        _disposalCancellationSource.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogDebug("Connecting to control pipe '{PipeName}'.", _pipeName);
            await _pipe.ConnectAsync(cancellationToken);

            using var reader = new StreamReader(_pipe);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    return;
                }

                var command = JsonSerializer.Deserialize<WatchControlCommand>(line);
                if (command is null)
                {
                    break;
                }

                if (command.Type == WatchControlCommand.Types.Rebuild)
                {
                    Logger.LogDebug("Received request to restart projects");
                    await RestartProjectsAsync(command.Projects.Select(ProjectRepresentation.FromProjectOrEntryPointFilePath), cancellationToken);
                }
                else
                {
                    Logger.LogError("Unknown control command: '{Type}'", command.Type);
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or IOException)
        {
            // expected when disposing or if the server disconnects
        }
        catch (Exception e)
        {
            Logger.LogDebug("Control pipe listener failed: {Message}", e.Message);
        }
    }

    private async ValueTask RestartProjectsAsync(IEnumerable<ProjectRepresentation> projects, CancellationToken cancellationToken)
    {
        var projectsToRestart = _launcher.RunningProjectsManager.GetRunningProjects(projects).ToArray();
        await _launcher.RunningProjectsManager.TerminatePeripheralProcessesAsync(projectsToRestart, cancellationToken);

        foreach (var project in projects)
        {
            if (!projectsToRestart.Any(p => p.Options.Representation == project))
            {
                Logger.LogDebug("Restart of '{Project}' requested but the project is not running.", project);
            }
        }

        await _launcher.RunningProjectsManager.RestartPeripheralProjectsAsync(projectsToRestart, cancellationToken);
    }
}
