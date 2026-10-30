// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Adapted from https://github.com/microsoft/testfx/blob/f4d8d35bf45ce0ece75a66e1b197c96684767e48/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/CursorProgressRenderer.cs

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class CursorProgressRenderer : IProgressRenderer
{
    public TimeSpan TickInterval => TimeSpan.FromMilliseconds(500);

    public void OnStart()
    {
    }

    public void OnTick(ITerminal terminal, TestProgressState?[] progressItems)
        => terminal.RenderProgress(progressItems);

    public void OnWrite(ITerminal terminal, TestProgressState?[] progressItems, Action<ITerminal> write)
    {
        terminal.EraseProgress();
        write(terminal);
        terminal.RenderProgress(progressItems);
    }

    public void OnTestCompleted()
    {
    }
}
