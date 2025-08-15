// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Immutable;

namespace Microsoft.DotNet.HotReload;

internal readonly struct HotReloadStaticAssetUpdate(string assemblyName, string relativePath, ImmutableArray<byte> content, bool isApplicationProject)
{
    public string RelativePath { get; } = relativePath;
    public string AssemblyName { get; } = assemblyName;
    public ImmutableArray<byte> Content { get; } = content;
    public bool IsApplicationProject { get; } = isApplicationProject;
}
