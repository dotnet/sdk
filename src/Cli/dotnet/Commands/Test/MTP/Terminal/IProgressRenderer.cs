// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Adapted from https://github.com/microsoft/testfx/blob/f4d8d35bf45ce0ece75a66e1b197c96684767e48/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/IProgressRenderer.cs

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal interface IProgressRenderer
{
    TimeSpan TickInterval { get; }
    void OnStart();
    void OnTick(ITerminal terminal, TestProgressState?[] progressItems);
    void OnWrite(ITerminal terminal, TestProgressState?[] progressItems, Action<ITerminal> write);

    // Called outside the rendering lock by the IPC message pump.
    void OnTestCompleted();
}
