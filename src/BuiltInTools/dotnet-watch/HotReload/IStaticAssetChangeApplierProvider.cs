// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal interface IStaticAssetChangeApplierProvider
{
    bool TryGetApplier(ProjectGraphNode projectNode, [NotNullWhen(true)] out IStaticAssetChangeApplier? applier);
}

internal interface IStaticAssetChangeApplier
{
}
