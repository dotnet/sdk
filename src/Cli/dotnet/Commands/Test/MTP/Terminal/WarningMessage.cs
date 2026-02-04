// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// A warning message that was sent during run.
/// </summary>
internal sealed record WarningMessage(string Text) : IProgressMessage;
