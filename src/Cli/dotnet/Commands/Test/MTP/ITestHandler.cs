// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal interface ITestHandler
{
    bool Initialize();

    /// <summary>
    /// All modules that will be run. Available after a successful <see cref="Initialize"/> so the
    /// results directory layout can be computed with knowledge of the whole run.
    /// </summary>
    IEnumerable<TestModule> EnumerateTestModules();

    IEnumerable<string?> GetTestApplicationWorkingDirectories();

    int RunTestApplications(TestApplicationActionQueue actionQueue);
}
