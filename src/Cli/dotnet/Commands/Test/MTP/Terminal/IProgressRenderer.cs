// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Adapted from https://github.com/microsoft/testfx/blob/b3f3c980c480c5d97976d658ac367b1e246502be/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/IProgressRenderer.cs

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

// OnWrite and OnTick hold the terminal lock. OnStart runs before the rendering thread starts,
// without that lock; TickInterval and OnTestCompleted are also accessed without the lock.
internal interface IProgressRenderer
{
    TimeSpan TickInterval { get; }
    void OnStart();
    void OnTick(ITerminal terminal, TestProgressState?[] progressItems);
    void OnWrite(ITerminal terminal, TestProgressState?[] progressItems, Action<ITerminal> write);

    // Called outside the rendering lock by the IPC message pump.
    void OnTestCompleted();
}
