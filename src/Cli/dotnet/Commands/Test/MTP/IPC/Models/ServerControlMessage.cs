// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

/// <param name="AggregateExitCode">Only for <see cref="ServerControlKinds.RunInvocationCleanup"/>.</param>
/// <param name="IsCanceled">Only for <see cref="ServerControlKinds.RunInvocationCleanup"/>.</param>
internal sealed record ServerControlMessage(byte Kind, int? AggregateExitCode = null, bool? IsCanceled = null) : IResponse;
