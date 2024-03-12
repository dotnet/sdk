// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class DotNetBuildFilter : IWatchFilter
    {
        private readonly IFileSetFactory _fileSetFactory;
        private readonly ProcessRunner _processRunner;
        private readonly IReporter _reporter;
        private readonly EnvironmentOptions _options;

        public DotNetBuildFilter(EnvironmentOptions options, IFileSetFactory fileSetFactory, ProcessRunner processRunner, IReporter reporter)
        {
            _fileSetFactory = fileSetFactory;
            _processRunner = processRunner;
            _reporter = reporter;
            _options = options;
        }

        public async ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken)
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

                if (context.Iteration == 0 || (context.ChangedFile?.FilePath is string changedFile && changedFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
                {
                    arguments.Add("/restore");
                }

                var processSpec = new ProcessSpec
                {
                    Executable = _options.MuxerPath,
                    Arguments = arguments,
                    WorkingDirectory = context.ProcessSpec.WorkingDirectory,
                };

                _reporter.Output("Building...", emoji: "🔧");
                var exitCode = await _processRunner.RunAsync(processSpec, cancellationToken);
                context.FileSet = await _fileSetFactory.CreateAsync(cancellationToken);
                if (exitCode == 0)
                {
                    return;
                }

                // If the build fails, we'll retry until we have a successful build.
                using var fileSetWatcher = new FileSetWatcher(context.FileSet, _reporter);
                await fileSetWatcher.GetChangedFileAsync(cancellationToken, () => _reporter.Warn("Waiting for a file to change before restarting dotnet...", emoji: "⏳"));
            }
        }
    }
}
