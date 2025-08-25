// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal abstract class WebApplicationAppModel(ProjectGraphNode? agentInjectionProject)
    : HotReloadAppModel(agentInjectionProject)
{
    // This needs to be in sync with the version BrowserRefreshMiddleware is compiled against.
    private static readonly Version s_minimumSupportedVersion = Versions.Version6_0;

    public bool IsServerSupported(ProjectGraphNode projectNode, EnvironmentOptions options, ILogger logger)
    {
        if (options.SuppressBrowserRefresh)
        {
            logger.Log(MessageDescriptor.SkippingConfiguringBrowserRefresh_SuppressedViaEnvironmentVariable.WithSeverityWhen(MessageSeverity.Error, RequiresBrowserRefresh), EnvironmentVariables.Names.SuppressBrowserRefresh);
            return false;
        }

        if (!projectNode.IsNetCoreApp(minVersion: s_minimumSupportedVersion))
        {
            logger.Log(MessageDescriptor.SkippingConfiguringBrowserRefresh_TargetFrameworkNotSupported.WithSeverityWhen(MessageSeverity.Error, RequiresBrowserRefresh));
            return false;
        }

        logger.Log(MessageDescriptor.ConfiguredToUseBrowserRefresh);
        return true;
    }
}
