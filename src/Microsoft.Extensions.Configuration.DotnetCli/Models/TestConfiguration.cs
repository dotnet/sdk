// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control test runner behavior.
/// </summary>
public sealed class TestConfiguration
{
    /// <summary>
    /// Gets or sets the test runner name to use.
    /// Mapped from dotnet.config file: [dotnet.test.runner] name=VALUE
    /// Defaults to "VSTest" if not specified.
    /// </summary>
    public string RunnerName { get; set; } = "VSTest";
}
