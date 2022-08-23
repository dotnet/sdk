// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.DotNet.DarcLib.VirtualMonoRepo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.VirtualMonoRepo.Tasks;

public class VirtualMonoRepo_Initialize : Build.Utilities.Task, ICancelableTask
{
    private readonly Lazy<IServiceProvider> _serviceProvider;
    private readonly CancellationTokenSource _cancellationToken = new();

    [Required]
    public string Repository { get; set; }

    [Required]
    public string VmrPath { get; set; }

    [Required]
    public string TmpPath { get; set; }

    public string Revision { get; set; }

    public VirtualMonoRepo_Initialize()
    {
        _serviceProvider = new(CreateServiceProvider);
    }

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

    private async Task<bool> ExecuteAsync()
    {
        var factory = _serviceProvider.Value.GetRequiredService<IVmrManagerFactory>();
        var vmrManager = await factory.CreateVmrManager(_serviceProvider.Value, VmrPath, TmpPath);
        await vmrManager.InitializeVmr(Repository, Revision, false, _cancellationToken.Token);
        return true;
    }

    public void Cancel() => _cancellationToken.Cancel();

    private IServiceProvider CreateServiceProvider() => new ServiceCollection()
        .AddLogging(b => b.AddConsole().AddFilter(l => l >= LogLevel.Information))
        .AddTransient<IProcessManager>(sp => ActivatorUtilities.CreateInstance<ProcessManager>(sp, "git"))
        .AddSingleton<ISourceMappingParser, SourceMappingParser>()
        .AddSingleton<IVmrManagerFactory, VmrManagerFactory>()
        .AddSingleton<IRemoteFactory>(sp => ActivatorUtilities.CreateInstance<RemoteFactory>(sp, TmpPath))
        .BuildServiceProvider();
}
