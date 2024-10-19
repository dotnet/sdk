// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.WebTools.AspireServer.Contracts;

/// <summary>
/// Interface implemented on the VS side and pass
/// </summary>
internal interface IAspireServerEvents
{
    /// <summary>
    /// Called when a request to stop a session is received. 
    /// </summary>
    /// <param name="dcpId">DCP/AppHost making the request. May be empty for older DCP versions.</param>
    ValueTask StopSessionAsync(string dcpId, string sessionId, CancellationToken cancelToken);

    /// <summary>
    /// Called when a request to start a project is received. Returns the sessionId of the started project.
    /// </summary>
    /// <param name="dcpId">DCP/AppHost making the request. May be empty for older DCP versions.</param>
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
