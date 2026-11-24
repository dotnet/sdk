// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Adapted from https://github.com/microsoft/testfx/blob/f4d8d35bf45ce0ece75a66e1b197c96684767e48/src/Platform/Microsoft.Testing.Platform/OutputDevice/SlowTestThresholdState.cs

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class SlowTestThresholdState
{
    private long _nextThresholdTicks;

    internal SlowTestThresholdState(TimeSpan initialThreshold)
        => _nextThresholdTicks = initialThreshold.Ticks;

    internal bool IsDue(TimeSpan elapsed)
    {
        long elapsedTicks = elapsed.Ticks;
        if (_nextThresholdTicks <= 0 || elapsedTicks < _nextThresholdTicks)
        {
            return false;
        }

        while (_nextThresholdTicks <= elapsedTicks)
        {
            if (_nextThresholdTicks > TimeSpan.MaxValue.Ticks / 2)
            {
                _nextThresholdTicks = TimeSpan.MaxValue.Ticks;
                break;
            }

            _nextThresholdTicks *= 2;
        }

        return true;
    }
}
