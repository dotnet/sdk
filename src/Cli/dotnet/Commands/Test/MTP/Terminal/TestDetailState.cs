// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TestDetailState(long id, IStopwatch? stopwatch, string text)
{
    private string _text = text;

    public long Id { get; } = id;

    public long Version { get; set; }

    public IStopwatch? Stopwatch { get; } = stopwatch;

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
