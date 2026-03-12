// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    Converters = new[] { typeof(ReleaseVersionJsonConverter) })]
[JsonSerializable(typeof(DotnetupManifestData))]
[JsonSerializable(typeof(DotnetRootEntry))]
[JsonSerializable(typeof(InstallSpec))]
[JsonSerializable(typeof(Installation))]
[JsonSerializable(typeof(InstallSource))]
[JsonSerializable(typeof(InstallComponent))]
[JsonSerializable(typeof(InstallArchitecture))]
[JsonSerializable(typeof(InstallType))]
[JsonSerializable(typeof(ManagementCadence))]
internal partial class DotnetupManifestJsonContext : JsonSerializerContext { }
