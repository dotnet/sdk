// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Marker interface that signals that this contains sub-manifests
/// </summary>
public interface IMultiImageManifest: IManifest;

public record struct ManifestListV2(int schemaVersion, string mediaType, PlatformSpecificManifest[] manifests) : IMultiImageManifest;

public record struct PlatformInformation(string architecture, string os, string? variant, string[] features, [property: JsonPropertyName("os.version")][field: JsonPropertyName("os.version")] string? version);

public record struct PlatformSpecificManifest(string mediaType, long size, string digest, PlatformInformation platform);
public record struct ImageIndexV1(int schemaVersion, string mediaType, PlatformSpecificOciManifest[] manifests) : IMultiImageManifest;

public record struct PlatformSpecificOciManifest(string mediaType, long size, string digest, PlatformInformation platform, Dictionary<string, string> annotations);
