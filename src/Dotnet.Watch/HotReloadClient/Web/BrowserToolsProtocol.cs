// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// The contract between the dotnet-watch browser tools provider and the browser tools client.
///
/// The client is generated into the application build output together with the public half of the
/// provider's session key, so the two always ship together and there is no version negotiation.
/// <see cref="RoutePrefix"/> must be kept in sync with the DotNetWatchBrowserToolsRoutePrefix
/// property in Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets.
/// </summary>
internal static class BrowserToolsProtocol
{
    public const string RoutePrefix = "/_framework/dotnet-browser-tools";
    public const string ClearCachePath = "/clear-cache";
    public const string ConnectPath = "/connect";
}

internal sealed record BrowserToolsManagedCodeUpdate(
    Guid ModuleId,
    byte[] MetadataDelta,
    byte[] ILDelta,
    byte[] PdbDelta,
    int[] UpdatedTypes);

/// <summary>
/// A batch of managed code updates. Batches are retained for the current application baseline so
/// that browsers connecting later can replay them before they receive any live message.
/// </summary>
internal sealed record BrowserToolsUpdateBatch(
    ImmutableArray<BrowserToolsManagedCodeUpdate> Deltas);
