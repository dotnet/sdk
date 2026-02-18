// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/
/// </remarks>
internal interface IRegistryAPI
{
    public IBlobOperations Blob { get; }

    public IManifestOperations Manifest { get; }
}
