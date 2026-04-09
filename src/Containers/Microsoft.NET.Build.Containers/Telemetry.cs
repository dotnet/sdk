// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.LocalDaemons;

namespace Microsoft.NET.Build.Containers;

internal class Telemetry
{
    /// <summary>
    /// Interesting data about the container publish - used to track the usage rates of various sources/targets of the process
    /// and to help diagnose issues with the container publish overall.
    /// </summary>
    /// <param name="RemotePullType">If the base image came from a remote registry, what kind of registry was it?</param>
    /// <param name="LocalPullType">If the base image came from a local store of some kind, what kind of store was it?</param>
    /// <param name="RemotePushType">If the new image is being pushed to a remote registry, what kind of registry is it?</param>
    /// <param name="LocalPushType">If the new image is being stored in a local store of some kind, what kind of store is it?</param>
    private record class PublishTelemetryContext(RegistryType? RemotePullType, LocalStorageType? LocalPullType, RegistryType? RemotePushType, LocalStorageType? LocalPushType);
    private enum RegistryType { Azure, AWS, Google, GitHub, DockerHub, MCR, Other }
    private enum LocalStorageType { Docker, Podman, Tarball }

    private readonly Microsoft.Build.Utilities.TaskLoggingHelper Log;
    private readonly PublishTelemetryContext Context;

    internal Telemetry(
        SourceImageReference source,
        DestinationImageReference destination,
        Microsoft.Build.Utilities.TaskLoggingHelper Log)
    {
        this.Log = Log;
        Context = new PublishTelemetryContext(
            source.Registry is not null ? GetRegistryType(source.Registry) : null,
            null, // we don't support local pull yet, but we may in the future
            destination.RemoteRegistry is not null ? GetRegistryType(destination.RemoteRegistry) : null,
            destination.LocalRegistry is not null ? GetLocalStorageType(destination.LocalRegistry) : null);
    }

    private RegistryType GetRegistryType(Registry r)
    {
        if (r.IsMcr) return RegistryType.MCR;
        if (r.IsGithubPackageRegistry) return RegistryType.GitHub;
        if (r.IsAmazonECRRegistry) return RegistryType.AWS;
        if (r.IsAzureContainerRegistry) return RegistryType.Azure;
        if (r.IsGoogleArtifactRegistry) return RegistryType.Google;
        if (r.IsDockerHub) return RegistryType.DockerHub;
        return RegistryType.Other;
    }

    private LocalStorageType GetLocalStorageType(ILocalRegistry r)
    {
        if (r is ArchiveFileRegistry) return LocalStorageType.Tarball;
        var d = r as DockerCli;
        System.Diagnostics.Debug.Assert(d != null, "Unknown local registry type");
        if (d.GetCommand() == DockerCli.DockerCommand) return LocalStorageType.Docker;
        else return LocalStorageType.Podman;
    }

    private IDictionary<string, string?> ContextProperties() => new Dictionary<string, string?>
        {
            { nameof(Context.RemotePullType), Context.RemotePullType?.ToString() },
            { nameof(Context.LocalPullType), Context.LocalPullType?.ToString() },
            { nameof(Context.RemotePushType), Context.RemotePushType?.ToString() },
            { nameof(Context.LocalPushType), Context.LocalPushType?.ToString() }
        };

    public void LogPublishSuccess()
    {
        Log.LogTelemetry("sdk/container/publish/success", ContextProperties());
    }

    public void LogUnknownRepository()
    {
        var props = ContextProperties();
        props.Add("error", "unknown_repository");
        Log.LogTelemetry("sdk/container/publish/error", props);
    }

    public void LogCredentialFailure(SourceImageReference _)
    {
        var props = ContextProperties();
        props.Add("error", "credential_failure");
        props.Add("direction", "pull");
        Log.LogTelemetry("sdk/container/publish/error", props);
    }

    public void LogCredentialFailure(DestinationImageReference d)
    {
        var props = ContextProperties();
        props.Add("error", "credential_failure");
        props.Add("direction", "push");
        Log.LogTelemetry("sdk/container/publish/error", props);
    }

    public void LogRidMismatch(string desiredRid, string[] availableRids)
    {
        var props = ContextProperties();
        props.Add("error", "rid_mismatch");
        props.Add("target_rid", desiredRid);
        props.Add("available_rids", string.Join(",", availableRids));
        Log.LogTelemetry("sdk/container/publish/error", props);
    }

    public void LogMissingLocalBinary()
    {
        var props = ContextProperties();
        props.Add("error", "missing_binary");
        Log.LogTelemetry("sdk/container/publish/error", props);
    }

    public void LogLocalLoadError()
    {
        var props = ContextProperties();
        props.Add("error", "local_load");
        Log.LogTelemetry("sdk/container/publish/error", props);
    }
}