// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed record SuccessfulTestResultMessage(string? Uid, string? DisplayName, byte? State, string? Reason, string? SessionUid);

    internal sealed record FailedTestResultMessage(string? Uid, string? DisplayName, byte? State, string? Reason, string? ErrorMessage, string? ErrorStackTrace, string? SessionUid);

    internal sealed record TestResultMessages(string? ExecutionId, SuccessfulTestResultMessage[] SuccessfulTestMessages, FailedTestResultMessage[] FailedTestMessages) : IRequest;
}
