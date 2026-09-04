// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestBrowserRefreshServer : AbstractBrowserRefreshServer
{
    public Func<WebServerHost>? CreateAndStartHostImpl;

    public TestBrowserRefreshServer(
        Action<IDictionary<string, string>, AbstractBrowserRefreshServer> configureLaunchEnvironment,
        SharedSecretProvider sharedSecretProvider)
        : base(configureLaunchEnvironment, sharedSecretProvider, new TestLogger(), _ => new TestLogger(), _ => new TestLogger())
    {
        SharedSecretProvider = sharedSecretProvider;
    }

    public TestBrowserRefreshServer(Action<IDictionary<string, string>, AbstractBrowserRefreshServer> configureLaunchEnvironment)
        : this(configureLaunchEnvironment, new SharedSecretProvider())
    {
    }

    public SharedSecretProvider SharedSecretProvider { get; }

    protected override ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult((CreateAndStartHostImpl ?? throw new NotImplementedException())());

    protected override bool SuppressTimeouts => true;
}
