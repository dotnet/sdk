// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class DotNetBuildFilter(DotNetWatchContext context, EnvironmentOptions options, IFileSetFactory fileSetFactory, ProcessRunner processRunner) : IWatchFilter
    {
        public async ValueTask ProcessAsync(WatchState state, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var arguments = new List<string>()
                {
                    "msbuild",
                    "/nologo",
                    "/t:Build"
                };

                if (context.TargetFramework != null)
                {
                    arguments.Add($"/p:TargetFramework={context.TargetFramework}");
                }

                if (context.BuildProperties != null)
                {
                    arguments.AddRange(context.BuildProperties.Select(p => $"/p:{p.name}={p.value}"));
                }

                if (state.Iteration == 0 || (state.ChangedFile?.FilePath is string changedFile && changedFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
                {
                    arguments.Add("/restore");
                }

                var buildProcessSpec = new ProcessSpec
                {
                    Executable = options.MuxerPath,
                    Arguments = arguments,
                    WorkingDirectory = state.ProcessSpec.WorkingDirectory,
                };

                context.Reporter.Output("Building...", emoji: "🔧");
                var exitCode = await processRunner.RunAsync(buildProcessSpec, cancellationToken);
                state.FileSet = await fileSetFactory.CreateAsync(waitOnError: true, cancellationToken);
                if (exitCode == 0)
                {
                    return;
                }

                Debug.Assert(state.FileSet != null);

                // If the build fails, we'll retry until we have a successful build.
                using var fileSetWatcher = new FileSetWatcher(state.FileSet, context.Reporter);
                await fileSetWatcher.GetChangedFileAsync(
                    () => context.Reporter.Warn("Waiting for a file to change before restarting dotnet...", emoji: "⏳"),
                    cancellationToken);
            }
        }
    }
}
