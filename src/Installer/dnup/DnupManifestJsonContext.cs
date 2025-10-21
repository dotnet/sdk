// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    [JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        Converters = new[] { typeof(DotnetVersionJsonConverter), typeof(ReleaseVersionJsonConverter) })]
    [JsonSerializable(typeof(List<DotnetInstall>))]
    [JsonSerializable(typeof(DotnetVersion))]
    [JsonSerializable(typeof(DotnetVersionType))]
    [JsonSerializable(typeof(InstallComponent))]
    [JsonSerializable(typeof(InstallArchitecture))]
    [JsonSerializable(typeof(InstallType))]
    [JsonSerializable(typeof(ManagementCadence))]
    public partial class DnupManifestJsonContext : JsonSerializerContext { }
}
