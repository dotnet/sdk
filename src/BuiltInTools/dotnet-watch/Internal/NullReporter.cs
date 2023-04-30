// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        public void Verbose(string message, string emoji = "⌚")
        { }

        public void Output(string message, string emoji = "⌚")
        { }

        public void Warn(string message, string emoji = "⌚")
        { }

        public void Error(string message, string emoji = "❌")
        { }
    }
}
