
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed class TaskProgressState : ProgressStateBase
{
    private string _text;

    public TaskProgressState(long id, string text, IStopwatch stopwatch)
        : base(id, stopwatch)
    {
        _text = text;
    }

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
