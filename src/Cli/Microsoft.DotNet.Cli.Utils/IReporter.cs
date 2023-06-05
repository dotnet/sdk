// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.Cli.Utils
{
    public interface IReporter
    {
        void WriteLine(string message);

        void WriteLine();

        void WriteLine(string format, params object?[] args);

        void Write(string message);
    }
}
