// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.Watch;

internal readonly struct ManagedCodeSnapshot
{
    public Solution Solution { get; init; }
    public ImmutableDictionary<string, ImmutableArray<ProjectInstance>> ProjectInstances { get; init; }
}
