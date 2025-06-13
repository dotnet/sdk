// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal readonly struct StaticAssetUpdate(string relativePath, string assemblyName, byte[] content, bool isApplicationProject)
{
    public string RelativePath { get; } = relativePath;
    public string AssemblyName { get; } = assemblyName;
    public byte[] Content { get; } = content;
    public bool IsApplicationProject { get; } = isApplicationProject;
}
