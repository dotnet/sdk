// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal interface ITestHandler
{
    bool Initialize();

    /// <summary>
    /// Number of test modules that will be enqueued by <see cref="RunTestApplications"/>.
    /// Only valid after a successful <see cref="Initialize"/>.
    /// </summary>
    int TotalTestModuleCount { get; }

    int RunTestApplications(TestApplicationActionQueue actionQueue);
}
