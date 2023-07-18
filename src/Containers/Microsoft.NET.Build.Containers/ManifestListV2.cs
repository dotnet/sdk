// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

internal record struct ManifestListV2(int schemaVersion, string mediaType, PlatformSpecificManifest[] manifests);

internal record struct PlatformInformation(string architecture, string os, string? variant, string[] features, [property: JsonPropertyName("os.version")][field: JsonPropertyName("os.version")] string? version);

internal record struct PlatformSpecificManifest(string mediaType, long size, string digest, PlatformInformation platform);
