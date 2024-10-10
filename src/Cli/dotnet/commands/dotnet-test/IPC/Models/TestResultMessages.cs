// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed record SuccessfulTestResultMessage(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, string? SessionUid);

    internal sealed record FailedTestResultMessage(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, string? ErrorMessage, string? ErrorStackTrace, string? SessionUid);

    internal sealed record TestResultMessages(string? ExecutionId, SuccessfulTestResultMessage[] SuccessfulTestMessages, FailedTestResultMessage[] FailedTestMessages) : IRequest;
}
