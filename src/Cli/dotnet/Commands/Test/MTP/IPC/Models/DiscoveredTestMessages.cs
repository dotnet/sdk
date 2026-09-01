// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed record TraitMessage(string Key, string Value);

internal sealed record DiscoveredTestMessage(
    string? Uid,
    string? DisplayName,
    string? FilePath,
    int? LineNumber,
    string? Namespace,
    string? TypeName,
    string? MethodName,
    string[] ParameterTypeFullNames,
    TraitMessage[] Traits);

internal sealed record DiscoveredTestMessages(string? ExecutionId, string? InstanceId, DiscoveredTestMessage[] DiscoveredMessages) : IRequest;
