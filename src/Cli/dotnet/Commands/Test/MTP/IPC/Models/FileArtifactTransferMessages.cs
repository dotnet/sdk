// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed record FileArtifactTransferRequest(
    string? ExecutionId,
    string? InstanceId,
    string? TransferId,
    string? FileName,
    long TotalLength,
    long Offset,
    byte[] Data,
    bool IsFinal) : IRequest
{
    internal const int MaximumChunkBytes = 1024 * 1024;
}

internal sealed record FileArtifactTransferResponse(
    long NextOffset,
    string? FullPath,
    string? Error) : IResponse;
