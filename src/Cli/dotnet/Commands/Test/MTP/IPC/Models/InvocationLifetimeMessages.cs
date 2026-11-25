// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

/// <summary>
/// An environment change produced by invocation setup. <see cref="HasValue"/> distinguishes removal from an empty value.
/// </summary>
/// <remarks>Values may be secrets. They must never be logged, traced, or echoed.</remarks>
internal sealed record InvocationLifetimeOutput(string Name, bool HasValue, string? Value)
{
    public override string ToString() => $"{nameof(InvocationLifetimeOutput)} {{ {nameof(Name)} = {Name}, {nameof(HasValue)} = {HasValue} }}";
}

internal sealed record InvocationSetupCompleted(
    string? ExecutionId,
    string? InstanceId,
    bool Succeeded,
    string? ErrorMessage,
    InvocationLifetimeOutput[] Outputs) : IRequest
{
    public override string ToString() => $"{nameof(InvocationSetupCompleted)} {{ {nameof(Succeeded)} = {Succeeded}, OutputCount = {Outputs.Length} }}";
}

internal sealed record ModuleTestsCompleted(string? ExecutionId, string? InstanceId, int ExitCode) : IRequest;

internal sealed record InvocationCleanupCompleted(
    string? ExecutionId,
    string? InstanceId,
    bool Succeeded,
    string? ErrorMessage) : IRequest;
