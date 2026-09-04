// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.HotReload;

internal sealed class GatewayProxyBrowserToolsLaunchConfigurator(Uri providerAddress) : IBrowserToolsLaunchConfigurator
{
    private const string RouteName = "dotnet-browser-tools";
    private const string ClusterName = "dotnet-browser-tools";

    public void ConfigureLaunchEnvironment(IDictionary<string, string> environment)
    {
        environment[$"ReverseProxy__Routes__{RouteName}__ClusterId"] = ClusterName;
        environment[$"ReverseProxy__Routes__{RouteName}__Order"] = "-1000";
        environment[$"ReverseProxy__Routes__{RouteName}__Match__Path"] = BrowserToolsProtocol.RoutePrefix + "/{**catch-all}";
        environment[$"ReverseProxy__Clusters__{ClusterName}__Destinations__provider__Address"] = providerAddress.AbsoluteUri;
    }
}
