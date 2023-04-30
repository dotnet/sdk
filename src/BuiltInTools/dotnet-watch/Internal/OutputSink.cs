// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class OutputSink
    {
        public OutputCapture Current { get; private set; }
        public OutputCapture StartCapture()
        {
            return (Current = new OutputCapture());
        }
    }
}