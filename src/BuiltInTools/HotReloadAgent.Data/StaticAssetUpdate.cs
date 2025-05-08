// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload;

internal readonly struct StaticAssetUpdate(
    string assemblyName,
    string relativePath,
    byte[] contents,
    bool isApplicationProject)
{
    public string AssemblyName { get; } = assemblyName;
    public bool IsApplicationProject { get; } = isApplicationProject;
    public string RelativePath { get; } = relativePath;
    public byte[] Contents { get; } = contents;
}
