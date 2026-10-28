// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Provides process execution and archive-transfer primitives shared by command-line container runtimes.
/// </summary>
internal sealed class ContainerRuntimeOperations
{
    private readonly ILogger _logger;
    private readonly Func<string, string, CancellationToken, Task<bool>> _tryRunCommand;
    private readonly ConcurrentDictionary<string, string> _commandPaths = new(StringComparer.Ordinal);

    public ContainerRuntimeOperations(
        ILogger logger,
        Func<string, string, CancellationToken, Task<bool>> tryRunCommand)
    {
        _logger = logger;
        _tryRunCommand = tryRunCommand;
    }

    public Task<bool> ProbeCommandAsync(string command, string arguments, CancellationToken cancellationToken)
        => _tryRunCommand(command, arguments, cancellationToken);

    public async Task LoadFromStandardInputAsync<T>(
        string command,
        string[] arguments,
        T image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Func<T, SourceImageReference, DestinationImageReference, Stream, CancellationToken, Task> writeStreamFunc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string commandPath = FindFullCommandPath(command);

        ProcessStartInfo loadInfo = new(commandPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            loadInfo.ArgumentList.Add(argument);
        }

        SafeFileHandle.CreateAnonymousPipe(out SafeFileHandle inputReadHandle, out SafeFileHandle inputWriteHandle, asyncWrite: true);
        using (inputReadHandle)
        await using (FileStream standardInput = new(inputWriteHandle, FileAccess.Write, bufferSize: 81920, isAsync: true))
        {
            loadInfo.StandardInputHandle = inputReadHandle;
            using CancellationTokenSource processCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task<ProcessTextOutput> loadTask = Process.RunAndCaptureTextAsync(loadInfo, processCancellation.Token);
            inputReadHandle.Close();
            ProcessTextOutput output;
            try
            {
                await writeStreamFunc(image, sourceReference, destinationReference, standardInput, cancellationToken)
                    .ConfigureAwait(false);
                await standardInput.DisposeAsync().ConfigureAwait(false);
                output = await loadTask.ConfigureAwait(false);
            }
            catch
            {
                await standardInput.DisposeAsync().ConfigureAwait(false);
                await processCancellation.CancelAsync().ConfigureAwait(false);
                await ((Task)loadTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                throw;
            }

            if (output.ExitStatus.ExitCode != 0)
            {
                throw new DockerLoadException(Resource.FormatString(nameof(Strings.ImageLoadFailed), output.StandardError));
            }
        }
    }

    public async Task LoadFromFileAsync<T>(
        string command,
        Func<string, string[]> getArguments,
        T image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Func<T, SourceImageReference, DestinationImageReference, Stream, CancellationToken, Task> writeStreamFunc,
        CancellationToken cancellationToken)
    {
        string commandPath = FindFullCommandPath(command);
        DirectoryInfo temporaryDirectory = Directory.CreateTempSubdirectory();
        string archivePath = Path.Combine(temporaryDirectory.FullName, "image.tar");
        try
        {
            await using (FileStream archive = new(
                archivePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await writeStreamFunc(image, sourceReference, destinationReference, archive, cancellationToken)
                    .ConfigureAwait(false);
            }

            (int exitCode, string stderr) = await RunProcessAsync(
                commandPath,
                getArguments(archivePath),
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new DockerLoadException(Resource.FormatString(nameof(Strings.ImageLoadFailed), stderr));
            }
        }
        finally
        {
            try
            {
                temporaryDirectory.Delete(recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Failed to delete temporary container image directory '{TemporaryDirectory}': {Message}",
                    temporaryDirectory.FullName,
                    ex.Message);
            }
        }
    }

    internal static async Task<bool> TryRunCommandAsync(string command, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            (int exitCode, _) = await RunProcessAsync(command, arguments, cancellationToken);
            return exitCode == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    internal string FindFullCommandPath(string command)
        => _commandPaths.GetOrAdd(command, FindFullPathFromPath);

    internal static string FindFullPathFromPath(string command)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(
                directory,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{command}.exe" : command);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return command;
    }

    internal static async Task<(int ExitCode, string StandardError)> RunProcessAsync(
        string commandPath,
        string arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(commandPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        return await RunProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int ExitCode, string StandardError)> RunProcessAsync(
        string commandPath,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(commandPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        return await RunProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int ExitCode, string StandardError)> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        ProcessTextOutput output = await Process.RunAndCaptureTextAsync(startInfo, cancellationToken).ConfigureAwait(false);
        return (output.ExitStatus.ExitCode, output.StandardError);
    }
}
