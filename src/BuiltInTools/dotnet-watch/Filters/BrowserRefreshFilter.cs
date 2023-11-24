// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class BrowserRefreshFilter : IWatchFilter, IAsyncDisposable
    {
        // This needs to be in sync with the version BrowserRefreshMiddleware is compiled against.
        private static readonly Version s_minimumSupportedVersion = new(6, 0);
        private readonly DotNetWatchOptions _options;
        private readonly IReporter _reporter;
        private readonly string _muxerPath;
        private BrowserRefreshServer? _refreshServer;

        public BrowserRefreshFilter(DotNetWatchOptions options, IReporter reporter, string muxerPath)
        {
            _options = options;
            _reporter = reporter;
            _muxerPath = muxerPath;
        }

        public async ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            Debug.Assert(context.ProcessSpec != null);

            if (_options.SuppressBrowserRefresh)
            {
                return;
            }

            if (context.Iteration == 0)
            {
                if (context.ProjectGraph is null)
                {
                    _reporter.Verbose("Unable to determine if this project is a webapp.");
                    return;
                }
                else if (!IsSupportedVersion(context.ProjectGraph))
                {
                    _reporter.Warning(
                        "Skipping configuring browser-refresh middleware since the target framework version is not supported." +
                        " For more information see 'https://aka.ms/dotnet/watch/unsupported-version'.");
                    return;
                }
                else if (IsWebApp(context.ProjectGraph))
                {
                    _reporter.Verbose("Configuring the app to use browser-refresh middleware.");
                }
                else
                {
                    _reporter.Verbose("Skipping configuring browser-refresh middleware since this is not a webapp.");
                    return;
                }

                _refreshServer = new BrowserRefreshServer(_options, context.Reporter, _muxerPath);
                context.BrowserRefreshServer = _refreshServer;
                var serverUrls = string.Join(',', await _refreshServer.StartAsync(cancellationToken));
                context.Reporter.Verbose($"Refresh server running at {serverUrls}.");
                context.ProcessSpec.EnvironmentVariables["ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT"] = serverUrls;
                context.ProcessSpec.EnvironmentVariables["ASPNETCORE_AUTO_RELOAD_WS_KEY"] = _refreshServer.ServerKey;

                var pathToMiddleware = Path.Combine(AppContext.BaseDirectory, "middleware", "Microsoft.AspNetCore.Watch.BrowserRefresh.dll");
                context.ProcessSpec.EnvironmentVariables.DotNetStartupHooks.Add(pathToMiddleware);
                context.ProcessSpec.EnvironmentVariables.AspNetCoreHostingStartupAssemblies.Add("Microsoft.AspNetCore.Watch.BrowserRefresh");
            }
            else if (!_options.SuppressBrowserRefresh)
            {
                // We've detected a change. Notify the browser.
                await (_refreshServer?.SendWaitMessageAsync(cancellationToken) ?? default);
            }
        }

        private bool IsSupportedVersion(ProjectGraph context)
        {
            if (context.GraphRoots.FirstOrDefault() is not { } projectNode)
            {
                return false;
            }
            if (projectNode.ProjectInstance.GetPropertyValue("_TargetFrameworkVersionWithoutV") is not string targetFrameworkVersion)
            {
                return false;
            }
            if(!Version.TryParse(targetFrameworkVersion, out var version))
            {
                return false;
            }

            return version >= s_minimumSupportedVersion;
        }

        private static bool IsWebApp(ProjectGraph projectGraph)
        {
            // We only want to enable browser refreshes if this is a WebApp (ASP.NET Core / Blazor app).
            return projectGraph.GraphRoots.FirstOrDefault() is { } projectNode &&
                projectNode.ProjectInstance.GetItems("ProjectCapability").Any(p => p.EvaluatedInclude is "AspNetCore" or "WebAssembly");
        }

        public async ValueTask DisposeAsync()
        {
            if (_refreshServer != null)
            {
                await _refreshServer.DisposeAsync();
            }
        }
    }
}
