// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.DotnetCli.Models;

namespace Microsoft.Extensions.Configuration.DotnetCli.Services;

/// <summary>
/// Interface for strongly-typed .NET CLI configuration service.
/// </summary>
public interface IDotNetConfigurationService
{
    /// <summary>
    /// Gets the underlying IConfiguration instance for advanced scenarios.
    /// </summary>
    IConfiguration RawConfiguration { get; }

    /// <summary>
    /// Gets CLI user experience configuration settings.
    /// </summary>
    CliUserExperienceConfiguration CliUserExperience { get; }

    /// <summary>
    /// Gets runtime host configuration settings.
    /// </summary>
    RuntimeHostConfiguration RuntimeHost { get; }

    /// <summary>
    /// Gets build and MSBuild configuration settings.
    /// </summary>
    BuildConfiguration Build { get; }

    /// <summary>
    /// Gets SDK resolver configuration settings.
    /// </summary>
    SdkResolverConfiguration SdkResolver { get; }

    /// <summary>
    /// Gets workload management configuration settings.
    /// </summary>
    WorkloadConfiguration Workload { get; }

    /// <summary>
    /// Gets first-time use experience configuration settings.
    /// </summary>
    FirstTimeUseConfiguration FirstTimeUse { get; }

    /// <summary>
    /// Gets development and debugging configuration settings.
    /// </summary>
    DevelopmentConfiguration Development { get; }

    /// <summary>
    /// Gets global tools configuration settings.
    /// </summary>
    ToolConfiguration Tool { get; }

    /// <summary>
    /// Gets NuGet package management configuration settings.
    /// </summary>
    NuGetConfiguration NuGet { get; }

    /// <summary>
    /// Gets test runner configuration settings.
    /// </summary>
    TestConfiguration Test { get; }
}
