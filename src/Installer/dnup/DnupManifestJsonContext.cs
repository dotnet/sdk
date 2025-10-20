// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using System.Collections.Generic;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    [JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        Converters = new[] { typeof(ReleaseVersionJsonConverter) })]
    [JsonSerializable(typeof(List<DotnetInstall>))]
    [JsonSerializable(typeof(InstallComponent))]
    [JsonSerializable(typeof(InstallArchitecture))]
    [JsonSerializable(typeof(InstallType))]
    [JsonSerializable(typeof(ManagementCadence))]
    internal partial class DnupManifestJsonContext : JsonSerializerContext { }
}
