// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.DotNet.DarcLib.VirtualMonoRepo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.VirtualMonoRepo.Tasks;

/// <summary>
/// This tasks equals calling the "darc vmr initialize" command.
/// This command pulls an individual repository into the VMR for the first time.
/// It can also recursively pull all of its dependencies based on Version.Details.xml.
/// </summary>
public class VirtualMonoRepo_Initialize : Build.Utilities.Task, ICancelableTask
{
    private readonly Lazy<IServiceProvider> _serviceProvider;
    private readonly CancellationTokenSource _cancellationToken = new();

    [Required]
    public string Repository { get; set; }

    [Required]
    public string SourceMappingsPath { get; set; }

    [Required]
    public string VmrPath { get; set; }

    [Required]
    public string TmpPath { get; set; }

    public string Revision { get; set; }

    public string PackageVersion { get; set; }

    public string SdkPath { get; set; }

    public string TpnTemplatePath { get; set; }

    public bool Recursive { get; set; }

    public VirtualMonoRepo_Initialize()
    {
        _serviceProvider = new(CreateServiceProvider);
    }

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

    private async Task<bool> ExecuteAsync()
    {
        VmrPath = Path.GetFullPath(VmrPath);
        TmpPath = Path.GetFullPath(TmpPath);

        var additionalRemotes = SdkPath == null
            ? Array.Empty<AdditionalRemote>()
            : new[] { new AdditionalRemote("sdk", SdkPath) };

        var vmrInitializer = _serviceProvider.Value.GetRequiredService<IVmrInitializer>();
        await vmrInitializer.InitializeRepository(
            Repository,
            Revision,
            PackageVersion,
            Recursive,
            new NativePath(SourceMappingsPath),
            additionalRemotes,
            null,
            TpnTemplatePath,
            generateCodeowners: false,
            discardPatches: true,
            _cancellationToken.Token);
        return true;
    }

    public void Cancel() => _cancellationToken.Cancel();

    private IServiceProvider CreateServiceProvider() => new ServiceCollection()
        .AddLogging(b => b.AddConsole().AddFilter(l => l >= LogLevel.Information))
        .AddVmrManagers("git", VmrPath, TmpPath, null, null)
        .BuildServiceProvider();
}
