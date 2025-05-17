// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Default model.
/// </summary>
internal sealed class DefaultAppModel : HotReloadAppModel
{
    public static readonly DefaultAppModel Instance = new();

    public override bool RequiresBrowserRefresh => false;
    public override bool InjectDeltaApplier => true;

    public override DeltaApplier? CreateDeltaApplier(BrowserRefreshServer? browserRefreshServer, IReporter processReporter)
        => new DefaultDeltaApplier(processReporter);
}
