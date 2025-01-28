// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal abstract class ProgressStateBase
{
    public ProgressStateBase(long id, IStopwatch stopwatch)
    {
        Id = id;
        Stopwatch = stopwatch;
    }

    public IStopwatch Stopwatch { get; }
    public TestNodeResultsState? TestNodeResultsState { get; internal set; }

    public long Id { get; internal set; }
    public long Version { get; internal set; }

    public int SlotIndex { get; internal set; }
}
