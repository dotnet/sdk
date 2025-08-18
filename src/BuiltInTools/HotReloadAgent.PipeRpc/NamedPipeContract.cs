// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

internal interface IRequest
{
    RequestType Type { get; }
    ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken);
}

internal interface IUpdateRequest : IRequest
{
}

internal enum RequestType
{
    ManagedCodeUpdate = 1,
    StaticAssetUpdate = 2,
    InitialUpdatesCompleted = 3,
}

internal readonly struct ManagedCodeUpdateRequest(IReadOnlyList<UpdateDelta> deltas, ResponseLoggingLevel responseLoggingLevel) : IUpdateRequest
{
    private const byte Version = 4;

    public IReadOnlyList<UpdateDelta> Deltas { get; } = deltas;
    public ResponseLoggingLevel ResponseLoggingLevel { get; } = responseLoggingLevel;
    public RequestType Type => RequestType.ManagedCodeUpdate;

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

internal readonly struct ClientInitializationResponse(string capabilities) 
{
    private const byte Version = 0;

    public string Capabilities { get; } = capabilities;

    public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Version, cancellationToken);
        await stream.WriteAsync(Capabilities, cancellationToken);
    }

    public static async ValueTask<ClientInitializationResponse> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var version = await stream.ReadByteAsync(cancellationToken);
        if (version != Version)
        {
            throw new NotSupportedException($"Unsupported version {version}.");
        }

        var capabilities = await stream.ReadStringAsync(cancellationToken);
        return new ClientInitializationResponse(capabilities);
    }
}

internal readonly struct StaticAssetUpdateRequest(
    string assemblyName,
    string relativePath,
    byte[] contents,
    bool isApplicationProject,
    ResponseLoggingLevel responseLoggingLevel) : IUpdateRequest
{
    private const byte Version = 2;

    public string AssemblyName { get; } = assemblyName;
    public bool IsApplicationProject { get; } = isApplicationProject;
    public string RelativePath { get; } = relativePath;
    public byte[] Contents { get; } = contents;
    public ResponseLoggingLevel ResponseLoggingLevel { get; } = responseLoggingLevel;

    public RequestType Type => RequestType.StaticAssetUpdate;

    public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Version, cancellationToken);
        await stream.WriteAsync(AssemblyName, cancellationToken);
        await stream.WriteAsync(IsApplicationProject, cancellationToken);
        await stream.WriteAsync(RelativePath, cancellationToken);
        await stream.WriteByteArrayAsync(Contents, cancellationToken);
        await stream.WriteAsync((byte)ResponseLoggingLevel, cancellationToken);
    }

    public static async ValueTask<StaticAssetUpdateRequest> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var version = await stream.ReadByteAsync(cancellationToken);
        if (version != Version)
        {
            throw new NotSupportedException($"Unsupported version {version}.");
        }

        var assemblyName = await stream.ReadStringAsync(cancellationToken);
        var isApplicationProject = await stream.ReadBooleanAsync(cancellationToken);
        var relativePath = await stream.ReadStringAsync(cancellationToken);
        var contents = await stream.ReadByteArrayAsync(cancellationToken);
        var responseLoggingLevel = (ResponseLoggingLevel)await stream.ReadByteAsync(cancellationToken);

        return new StaticAssetUpdateRequest(
            assemblyName: assemblyName,
            relativePath: relativePath,
            contents: contents,
            isApplicationProject,
            responseLoggingLevel);
    }
}
