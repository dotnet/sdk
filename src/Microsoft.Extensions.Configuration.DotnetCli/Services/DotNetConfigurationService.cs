// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration.DotnetCli.Models;

namespace Microsoft.Extensions.Configuration.DotnetCli.Services;

/// <summary>
/// Strongly-typed configuration service for .NET CLI with lazy initialization.
/// </summary>
public class DotNetConfigurationService : IDotNetConfigurationService
{
    private readonly IConfiguration _configuration;

    // Lazy initialization for each configuration section
    private readonly Lazy<CliUserExperienceConfiguration> _cliUserExperience;
    private readonly Lazy<RuntimeHostConfiguration> _runtimeHost;
    private readonly Lazy<BuildConfiguration> _build;
    private readonly Lazy<SdkResolverConfiguration> _sdkResolver;
    private readonly Lazy<WorkloadConfiguration> _workload;
    private readonly Lazy<FirstTimeUseConfiguration> _firstTimeUse;
    private readonly Lazy<DevelopmentConfiguration> _development;
    private readonly Lazy<ToolConfiguration> _tool;

    public IConfiguration RawConfiguration => _configuration;

    // Lazy-loaded strongly-typed configuration properties
    public CliUserExperienceConfiguration CliUserExperience => _cliUserExperience.Value;
    public RuntimeHostConfiguration RuntimeHost => _runtimeHost.Value;
    public BuildConfiguration Build => _build.Value;
    public SdkResolverConfiguration SdkResolver => _sdkResolver.Value;
    public WorkloadConfiguration Workload => _workload.Value;
    public FirstTimeUseConfiguration FirstTimeUse => _firstTimeUse.Value;
    public DevelopmentConfiguration Development => _development.Value;
    public ToolConfiguration Tool => _tool.Value;

    public DotNetConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Initialize lazy factories - configuration binding only happens on first access
        _cliUserExperience = new Lazy<CliUserExperienceConfiguration>(() =>
            _configuration.GetSection("CliUserExperience").Get<CliUserExperienceConfiguration>() ?? new());
        _runtimeHost = new Lazy<RuntimeHostConfiguration>(() =>
            _configuration.GetSection("RuntimeHost").Get<RuntimeHostConfiguration>() ?? new());
        _build = new Lazy<BuildConfiguration>(() =>
            _configuration.GetSection("Build").Get<BuildConfiguration>() ?? new());
        _sdkResolver = new Lazy<SdkResolverConfiguration>(() =>
            _configuration.GetSection("SdkResolver").Get<SdkResolverConfiguration>() ?? new());
        _workload = new Lazy<WorkloadConfiguration>(() =>
            _configuration.GetSection("Workload").Get<WorkloadConfiguration>() ?? new());
        _firstTimeUse = new Lazy<FirstTimeUseConfiguration>(() =>
            _configuration.GetSection("FirstTimeUse").Get<FirstTimeUseConfiguration>() ?? new());
        _development = new Lazy<DevelopmentConfiguration>(() =>
            _configuration.GetSection("Development").Get<DevelopmentConfiguration>() ?? new());
        _tool = new Lazy<ToolConfiguration>(() =>
            _configuration.GetSection("Tool").Get<ToolConfiguration>() ?? new());
    }
}
