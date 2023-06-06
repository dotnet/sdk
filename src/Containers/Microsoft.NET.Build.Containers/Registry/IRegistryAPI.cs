// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.Registry;

internal interface IRegistryAPI
{
    public IManifestOperations Manifest { get; }
    public IBlobOperations Blob { get; }
}
