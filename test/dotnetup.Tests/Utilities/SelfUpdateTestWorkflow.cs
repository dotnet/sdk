// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class SelfUpdateTestWorkflow : SelfUpdateWorkflow
{
    public SelfUpdateTestWorkflow(SelfUpdatePaths paths, string loadedIdentity, Func<ResolvedDownload> resolve,
        Action<ResolvedDownload, string> download, SelfUpdateCoordinator? coordinator = null)
        : base(paths, loadedIdentity, resolve, download, coordinator)
    {
    }

    public Action<string>? VerifyAction { get; init; }

    public int VerificationCount { get; private set; }

    public string? Execute() => ExecuteAndReleaseLocks(this);

    public static string? ExecuteAndReleaseLocks(SelfUpdateWorkflow workflow)
    {
        IDisposable? locks = null;
        try
        {
            return workflow.Execute(lease => locks = lease);
        }
        finally
        {
            locks?.Dispose();
        }
    }

    protected override void Verify(string installedPath)
    {
        VerificationCount++;
        VerifyAction?.Invoke(installedPath);
    }
}