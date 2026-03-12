// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed record FileArtifactMessage(string? FullPath, string? DisplayName, string? Description, string? TestUid, string? TestDisplayName, string? SessionUid);

internal sealed record FileArtifactMessages(string? ExecutionId, string? InstanceId, FileArtifactMessage[] FileArtifacts) : IRequest;
