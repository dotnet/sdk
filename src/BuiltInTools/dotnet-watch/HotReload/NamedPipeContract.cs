// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch;

internal interface IRequest
{
    ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken);
}

internal enum RequestType
{
    ManagedCodeUpdate = 1,
    StaticAssetUpdate = 2,
    InitialUpdatesCompleted = 3,
}

internal readonly struct ManagedCodeUpdateRequest(IReadOnlyList<UpdateDelta> deltas, ResponseLoggingLevel responseLoggingLevel) : IRequest
{
    private const byte Version = 4;

    public IReadOnlyList<UpdateDelta> Deltas { get; } = deltas;
    public ResponseLoggingLevel ResponseLoggingLevel { get; } = responseLoggingLevel;

    /// <summary>
    /// Called by the dotnet-watch.
    /// </summary>
    public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Version, cancellationToken);
        await stream.WriteAsync(Deltas.Count, cancellationToken);

        foreach (var delta in Deltas)
        {
            await stream.WriteAsync(delta.ModuleId, cancellationToken);
            await stream.WriteByteArrayAsync(delta.MetadataDelta, cancellationToken);
            await stream.WriteByteArrayAsync(delta.ILDelta, cancellationToken);
            await stream.WriteByteArrayAsync(delta.PdbDelta, cancellationToken);
            await stream.WriteAsync(delta.UpdatedTypes, cancellationToken);
        }

        await stream.WriteAsync((byte)ResponseLoggingLevel, cancellationToken);
    }

    /// <summary>
    /// Called by delta applier.
    /// </summary>
    public static async ValueTask<ManagedCodeUpdateRequest> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var version = await stream.ReadByteAsync(cancellationToken);
        if (version != Version)
        {
            throw new NotSupportedException($"Unsupported version {version}.");
        }

        var count = await stream.ReadInt32Async(cancellationToken);

        var deltas = new UpdateDelta[count];
        for (var i = 0; i < count; i++)
        {
            var moduleId = await stream.ReadGuidAsync(cancellationToken);
            var metadataDelta = await stream.ReadByteArrayAsync(cancellationToken);
            var ilDelta = await stream.ReadByteArrayAsync(cancellationToken);
            var pdbDelta = await stream.ReadByteArrayAsync(cancellationToken);
            var updatedTypes = await stream.ReadIntArrayAsync(cancellationToken);

            deltas[i] = new UpdateDelta(moduleId, metadataDelta: metadataDelta, ilDelta: ilDelta, pdbDelta: pdbDelta, updatedTypes);
        }

        var responseLoggingLevel = (ResponseLoggingLevel)await stream.ReadByteAsync(cancellationToken);
        return new ManagedCodeUpdateRequest(deltas, responseLoggingLevel: responseLoggingLevel);
    }
}

internal readonly struct UpdateResponse(IReadOnlyCollection<(string message, AgentMessageSeverity severity)> log, bool success)
{
    public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(success, cancellationToken);
        await stream.WriteAsync(log.Count, cancellationToken);

        foreach (var (message, severity) in log)
        {
            await stream.WriteAsync(message, cancellationToken);
            await stream.WriteAsync((byte)severity, cancellationToken);
        }
    }

    public static async ValueTask<(bool success, IAsyncEnumerable<(string message, AgentMessageSeverity severity)>)> ReadAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        var success = await stream.ReadBooleanAsync(cancellationToken);
        var log = ReadLogAsync(cancellationToken);
        return (success, log);

        async IAsyncEnumerable<(string message, AgentMessageSeverity severity)> ReadLogAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var entryCount = await stream.ReadInt32Async(cancellationToken);

            for (var i = 0; i < entryCount; i++)
            {
                var message = await stream.ReadStringAsync(cancellationToken);
                var severity = (AgentMessageSeverity)await stream.ReadByteAsync(cancellationToken);
                yield return (message, severity);
            }
        }
    }
}

internal readonly struct ClientInitializationRequest(string capabilities) : IRequest
{
    private const byte Version = 0;

    public string Capabilities { get; } = capabilities;

    /// <summary>
    /// Called by delta applier.
    /// </summary>
    public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Version, cancellationToken);
        await stream.WriteAsync(Capabilities, cancellationToken);
    }

    /// <summary>
    /// Called by dotnet-watch.
    /// </summary>
    public static async ValueTask<ClientInitializationRequest> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var version = await stream.ReadByteAsync(cancellationToken);
        if (version != Version)
        {
            throw new NotSupportedException($"Unsupported version {version}.");
        }

        var capabilities = await stream.ReadStringAsync(cancellationToken);
        return new ClientInitializationRequest(capabilities);
    }
}

internal readonly struct StaticAssetUpdateRequest(
    string assemblyName,
    string relativePath,
    byte[] contents,
    bool isApplicationProject) : IRequest
{
    private const byte Version = 1;

    public string AssemblyName { get; } = assemblyName;
    public bool IsApplicationProject { get; } = isApplicationProject;
    public string RelativePath { get; } = relativePath;
    public byte[] Contents { get; } = contents;

    public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Version, cancellationToken);
        await stream.WriteAsync(AssemblyName, cancellationToken);
        await stream.WriteAsync(IsApplicationProject, cancellationToken);
        await stream.WriteAsync(RelativePath, cancellationToken);
        await stream.WriteByteArrayAsync(Contents, cancellationToken);
    }

    public static async ValueTask<StaticAssetUpdateRequest> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var version = await stream.ReadByteAsync(cancellationToken);
        if (version != Version)
        {
            throw new NotSupportedException($"Unsupported version {version}.");
        }

        var assemblyName = await stream.ReadStringAsync(cancellationToken);
        var isAppProject = await stream.ReadBooleanAsync(cancellationToken);
        var relativePath = await stream.ReadStringAsync(cancellationToken);
        var contents = await stream.ReadByteArrayAsync(cancellationToken);

        return new StaticAssetUpdateRequest(
            assemblyName: assemblyName,
            relativePath: relativePath,
            contents: contents,
            isApplicationProject: isAppProject);
    }
}
