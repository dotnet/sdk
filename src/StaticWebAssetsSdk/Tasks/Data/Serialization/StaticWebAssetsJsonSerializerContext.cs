// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[JsonSerializable(typeof(StaticWebAssetsManifest))]
[JsonSerializable(typeof(GenerateStaticWebAssetsDevelopmentManifest.StaticWebAssetsDevelopmentManifest))]
[JsonSerializable(typeof(StaticWebAssetEndpointsManifest))]
public partial class StaticWebAssetsJsonSerializerContext : JsonSerializerContext
{
    // Since the manifest is only used at development time, it's ok for it to use the relaxed
    // json escaping (which is also what MVC uses by default)
    private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static readonly StaticWebAssetsJsonSerializerContext RelaxedEscaping = new(ManifestSerializationOptions);
}
