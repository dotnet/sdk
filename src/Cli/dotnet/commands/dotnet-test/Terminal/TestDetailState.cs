// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class TestDetailState
{
    private string _text;

    public TestDetailState(long id, IStopwatch? stopwatch, string text)
    {
        Id = id;
        Stopwatch = stopwatch;
        _text = text;
    }

    public long Id { get; }

    public long Version { get; set; }

    public IStopwatch? Stopwatch { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (!_text.Equals(value, StringComparison.Ordinal))
            {
                Version++;
                _text = value;
            }
        }
    }
}
