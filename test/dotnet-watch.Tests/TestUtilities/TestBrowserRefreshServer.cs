// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestBrowserRefreshServer : AbstractBrowserRefreshServer
{
    public Func<WebServerHost>? CreateAndStartHostImpl;

    public TestBrowserRefreshServer(
        Action<IDictionary<string, string>, AbstractBrowserRefreshServer> configureLaunchEnvironment,
        Func<SharedSecretProvider> sessionKeyFactory)
        : base(configureLaunchEnvironment, sessionKeyFactory, new TestLogger(), _ => new TestLogger(), _ => new TestLogger())
    {
    }

    public TestBrowserRefreshServer(Action<IDictionary<string, string>, AbstractBrowserRefreshServer> configureLaunchEnvironment)
        : this(configureLaunchEnvironment, static () => new SharedSecretProvider())
    {
    }

    protected override ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult((CreateAndStartHostImpl ?? throw new NotImplementedException())());

    protected override bool SuppressTimeouts => true;
}
