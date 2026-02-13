// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WebTools.AspireServer.Contracts;

/// <summary>
/// Interface implemented on the VS side and pass
/// </summary>
internal interface IAspireServerEvents
{
    /// <summary>
    /// Called when a request to stop a session is received. Returns false if the session does not exist. Note that the dcpId identifies
    /// which DCP/AppHost is making the request.
    /// </summary>
    ValueTask<bool> StopSessionAsync(string dcpId, string sessionId, CancellationToken cancelToken);

    /// <summary>
    /// Called when a request to start a project is received. Returns the sessionId of the started project. Note that the dcpId identifies
    /// which DCP/AppHost is making the request. The format of this string is <appHostAssemblyName>;<unique string>. The first token can
    /// be used to identify the AppHost project in the solution. The 2nd is just a unique string so that running the same project multiple times
    /// generates a unique dcpId. Note that for older DCP's the dcpId will be the empty string
    /// </summary>
    ValueTask<string> StartProjectAsync(string dcpId, ProjectLaunchRequest projectLaunchInfo, CancellationToken cancelToken);
}

internal class ProjectLaunchRequest
{
    public string ProjectPath { get; set; } = string.Empty;
    public bool Debug { get; set; }
    public IEnumerable<KeyValuePair<string, string>>? Environment { get; set; }
    public IEnumerable<string>? Arguments { get; set; }
    public string? LaunchProfile { get; set; }
    public bool DisableLaunchProfile { get; set; }
}
