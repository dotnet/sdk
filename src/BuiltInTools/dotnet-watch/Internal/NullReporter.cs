﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal sealed class NullReporter : IReporter
    {
        private NullReporter()
        { }

        public static IReporter Singleton { get; } = new NullReporter();

        public bool ReportProcessOutput => false;

        public void ProcessOutput(string projectPath, string data) => throw new InvalidOperationException();

        public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        {
        }
    }
}
