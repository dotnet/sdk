// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

public record struct ManifestListV2(int schemaVersion, string mediaType, PlatformSpecificManifest[] manifests);

public record struct PlatformInformation(string architecture, string os, string? variant, string[] features, [property: JsonPropertyName("os.version")][field: JsonPropertyName("os.version")] string? version);

public record struct PlatformSpecificManifest(string mediaType, long size, string digest, PlatformInformation platform);
