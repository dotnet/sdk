// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// An error message that was sent to output during the build.
/// </summary>
internal sealed record ErrorMessage(string Text) : IProgressMessage;
