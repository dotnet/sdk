// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(GlobalSettingsData))]
    internal partial class GlobalSettingsJsonSerializerContext : JsonSerializerContext;
}
