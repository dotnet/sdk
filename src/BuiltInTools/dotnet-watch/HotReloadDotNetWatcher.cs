// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class HotReloadDotNetWatcher : Watcher
    {
        private readonly DotNetWatchContext _context;
        private readonly IConsole _console;
        private readonly ProcessRunner _processRunner;
        private readonly RudeEditDialog? _rudeEditDialog;
        private readonly FileSetFactory _fileSetFactory;

        public HotReloadDotNetWatcher(DotNetWatchContext context, IConsole console, FileSetFactory fileSetFactory)
        {
            _context = context;
            _processRunner = new ProcessRunner(context.Reporter);
            _console = console;
            _fileSetFactory = fileSetFactory;

            if (!context.Options.NonInteractive)
            {
                var consoleInput = new ConsoleInputReader(_console, context.Options.Quiet, context.EnvironmentOptions.SuppressEmojis);
                _rudeEditDialog = new RudeEditDialog(context.Reporter, consoleInput, _console);
            }
        }

        public async Task WatchAsync(ProcessSpec processSpec, CancellationToken cancellationToken)
        {
            Debug.Assert(_context.ProjectGraph != null);

            var forceReload = new CancellationTokenSource();
            var hotReloadEnabledMessage = "Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.";

            if (!_context.Options.NonInteractive)
            {
                _context.Reporter.Output($"{hotReloadEnabledMessage}{Environment.NewLine}  {(_context.EnvironmentOptions.SuppressEmojis ? string.Empty : "💡")} Press \"Ctrl + R\" to restart.", emoji: "🔥");

                _console.KeyPressed += (key) =>
                {
                    var modifiers = ConsoleModifiers.Control;
                    if ((key.Modifiers & modifiers) == modifiers && key.Key == ConsoleKey.R)
                    {
                        var cancellationTokenSource = Interlocked.Exchange(ref forceReload, new CancellationTokenSource());
                        cancellationTokenSource.Cancel();
                    }
                };
            }
            else
            {
                _context.Reporter.Output(hotReloadEnabledMessage, emoji: "🔥");
            }

            var environmentBuilder = EnvironmentVariablesBuilder.FromCurrentEnvironment();
            var namedPipeName = Guid.NewGuid().ToString();

            // Configure the app for Hot Reload:
            environmentBuilder.DotNetStartupHooks.Add(Path.Combine(AppContext.BaseDirectory, "hotreload", "Microsoft.Extensions.DotNetDeltaApplier.dll"));
            environmentBuilder.Add(EnvironmentVariables.Names.DotnetModifiableAssemblies, "debug");
            environmentBuilder.Add(EnvironmentVariables.Names.DotnetHotReloadNamedPipeName, namedPipeName);

            processSpec.Executable = _context.EnvironmentOptions.MuxerPath;

            await using var browserConnector = new BrowserConnector(_context);

            for (var iteration = 0;;iteration++)
            {
                var (project, files) = await _fileSetFactory.CreateAsync(cancellationToken);

                if (!project.IsNetCoreApp60OrNewer())
                {
                    _context.Reporter.Error($"Hot reload based watching is only supported in .NET 6.0 or newer apps. Update the project's launchSettings.json to disable this feature.");
                    return;
                }

                await UpdateBrowserAsync(iteration, processSpec, environmentBuilder, browserConnector, project, cancellationToken);

                if (iteration == 0)
                {
                    processSpec.Arguments = [..environmentBuilder.ToCommandLineDirectives(), ..processSpec.Arguments ?? []];
                }

                processSpec.EnvironmentVariables[EnvironmentVariables.Names.DotnetWatchIteration] = (iteration + 1).ToString(CultureInfo.InvariantCulture);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                using var currentRunCancellationSource = new CancellationTokenSource();
                using var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    currentRunCancellationSource.Token,
                    forceReload.Token);
                using var fileSetWatcher = new HotReloadFileSetWatcher(files, _context.Reporter);

                try
                {
                    using var hotReload = new HotReload(_context.Reporter, _context.ProjectGraph, browserConnector.RefreshServer);

                    // Solution must be initialized before we start watching for file changes to avoid race condition
                    // when the solution captures state of the file after the changes has already been made.
                    await hotReload.InitializeAsync(project, namedPipeName, cancellationToken);

                    var processTask = _processRunner.RunAsync(processSpec, combinedCancellationSource.Token);
                    _context.Reporter.Output("Started");

                    Task<FileItem[]?> fileSetTask;
                    Task finishedTask;

                    while (true)
                    {
                        fileSetTask = fileSetWatcher.GetChangedFilesAsync(combinedCancellationSource.Token);
                        finishedTask = await Task.WhenAny(processTask, fileSetTask).WaitAsync(combinedCancellationSource.Token);

                        if (finishedTask != fileSetTask || fileSetTask.Result is not FileItem[] fileItems)
                        {
                            if (processTask.IsFaulted && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
                            {
                                // Only show this error message if the process exited non-zero due to a normal process exit.
                                // Don't show this if dotnet-watch killed the inner process due to file change or CTRL+C by the user
                                _context.Reporter.Error($"Application failed to start: {processTask.Exception?.InnerException?.Message}");
                            }
                            break;
                        }
                        else
                        {
                            if (MayRequireRecompilation(_context.ProjectGraph, fileItems) is { } newFile)
                            {
                                _context.Reporter.Output($"New file: {GetRelativeFilePath(newFile.FilePath)}. Rebuilding the application.");
                                break;
                            }
                            else if (fileItems.All(f => f.IsNewFile))
                            {
                                // If every file is a new file and none of them need to be compiled, keep moving.
                                continue;
                            }

                            if (fileItems.Length > 1)
                            {
                                // Filter out newly added files from the list to make the reporting cleaner.
                                // Any action we needed to take on significant newly added files is handled by MayRequiredRecompilation.
                                fileItems = fileItems.Where(f => !f.IsNewFile).ToArray();
                            }

                            if (fileItems.Length == 1)
                            {
                                _context.Reporter.Output($"File changed: {GetRelativeFilePath(fileItems[0].FilePath)}.");
                            }
                            else
                            {
                                _context.Reporter.Output($"Files changed: {string.Join(", ", fileItems.Select(f => GetRelativeFilePath(f.FilePath)))}");
                            }

                            var start = Stopwatch.GetTimestamp();
                            if (await hotReload.TryHandleFileChange(_context, fileItems, combinedCancellationSource.Token))
                            {
                                var totalTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - start);
                                _context.Reporter.Verbose($"Hot reload change handled in {totalTime.TotalMilliseconds}ms.", emoji: "🔥");
                            }
                            else
                            {
                                if (_rudeEditDialog is not null)
                                {
                                    await _rudeEditDialog.EvaluateAsync(combinedCancellationSource.Token);
                                }
                                else
                                {
                                    _context.Reporter.Verbose("Restarting without prompt since dotnet-watch is running in non-interactive mode.");
                                }
                                break;
                            }
                        }
                    }

                    // Regardless of the which task finished first, make sure everything is cancelled
                    // and wait for dotnet to exit. We don't want orphan processes
                    currentRunCancellationSource.Cancel();

                    await Task.WhenAll(processTask, fileSetTask);

                    if (!processTask.IsFaulted && processTask.Result != 0 && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
                    {
                        // Only show this error message if the process exited non-zero due to a normal process exit.
                        // Don't show this if dotnet-watch killed the inner process due to file change or CTRL+C by the user
                        _context.Reporter.Error($"Exited with error code {processTask.Result}");
                    }
                    else
                    {
                        _context.Reporter.Output("Exited");
                    }

                    if (finishedTask == processTask)
                    {
                        // Now wait for a file to change before restarting process
                        _context.Reporter.Warn("Waiting for a file to change before restarting dotnet...", emoji: "⏳");
                        await fileSetWatcher.GetChangedFilesAsync(cancellationToken, forceWaitForNewUpdate: true);
                    }
                    else
                    {
                        Debug.Assert(finishedTask == fileSetTask);
                    }
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException)
                    {
                        _context.Reporter.Verbose($"Caught top-level exception from hot reload: {e}");
                    }

                    if (!currentRunCancellationSource.IsCancellationRequested)
                    {
                        currentRunCancellationSource.Cancel();
                    }

                    if (forceReload.IsCancellationRequested)
                    {
                        _console.Clear();
                        _context.Reporter.Output("Restart requested.", emoji: "🔄");
                    }
                }
            }
        }

        private static FileItem? MayRequireRecompilation(ProjectGraph projectGraph, FileItem[] fileInfo)
        {
            // This method is invoked when a new file is added to the workspace. To determine if we need to
            // recompile, we'll see if it's any of the usual suspects (.cs, .cshtml, .razor) files.

            foreach (var file in fileInfo)
            {
                if (!file.IsNewFile || file.IsStaticFile)
                {
                    continue;
                }

                var filePath = file.FilePath;

                if (filePath is null)
                {
                    continue;
                }

                if (filePath.EndsWith(".cs", StringComparison.Ordinal) || filePath.EndsWith(".razor", StringComparison.Ordinal))
                {
                    return file;
                }

                if (filePath.EndsWith(".cshtml", StringComparison.Ordinal) &&
                    projectGraph.GraphRoots.FirstOrDefault() is { } project &&
                    project.ProjectInstance.GetPropertyValue("AddCshtmlFilesToDotNetWatchList") is not "false")
                {
                    // For cshtml files, runtime compilation can opt out of watching cshtml files.
                    // Obviously this does not work if a user explicitly removed files out of the watch list,
                    // but we could wait for someone to report it before we think about ways to address it.
                    return file;
                }

                if (filePath.EndsWith(".razor.css", StringComparison.Ordinal) || filePath.EndsWith(".cshtml.css", StringComparison.Ordinal))
                {
                    return file;
                }
            }

            return default;
        }

        private string GetRelativeFilePath(string path)
        {
            var relativePath = path;
            var workingDirectory = _context.EnvironmentOptions.WorkingDirectory;
            if (path.StartsWith(workingDirectory, StringComparison.Ordinal) && path.Length > workingDirectory.Length)
            {
                relativePath = path.Substring(workingDirectory.Length);

                return $".{(relativePath.StartsWith(Path.DirectorySeparatorChar) ? string.Empty : Path.DirectorySeparatorChar)}{relativePath}";
            }

            return relativePath;
        }
    }
}
