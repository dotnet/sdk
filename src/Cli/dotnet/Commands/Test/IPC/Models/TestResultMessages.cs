// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed record SuccessfulTestResultMessage(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, string? StandardOutput, string? ErrorOutput, string? SessionUid);

internal sealed record FailedTestResultMessage(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, ExceptionMessage[]? Exceptions, string? StandardOutput, string? ErrorOutput, string? SessionUid);

internal sealed record ExceptionMessage(string? ErrorMessage, string? ErrorType, string? StackTrace);

internal sealed record TestResultMessages(string? ExecutionId, string? InstanceId, SuccessfulTestResultMessage[] SuccessfulTestMessages, FailedTestResultMessage[] FailedTestMessages) : IRequest;
