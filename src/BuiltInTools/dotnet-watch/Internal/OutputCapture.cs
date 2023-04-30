// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class OutputCapture
    {
        private readonly List<string> _lines = new List<string>();
        public IEnumerable<string> Lines => _lines;
        public void AddLine(string line) => _lines.Add(line);
    }
}