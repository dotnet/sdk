// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed record DisplayMessage(string? ExecutionId, string? InstanceId, byte Level, string? Text) : IRequest;
