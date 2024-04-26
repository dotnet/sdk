// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class BufferedReporter : IReporter
    {
        public List<string> Lines { get; private set; } = new List<string>();

        public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine() => Lines.Add("");

        public void Write(string message) => throw new NotImplementedException();
    }
}
