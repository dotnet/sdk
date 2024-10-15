// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class DotNetWatchContext
    {
        public required GlobalOptions Options { get; init; }
        public required EnvironmentOptions EnvironmentOptions { get; init; }
        public required IReporter Reporter { get; init; }

        public ProjectGraph? ProjectGraph { get; init; }
        public required ProjectOptions RootProjectOptions { get; init; }
    }
}
