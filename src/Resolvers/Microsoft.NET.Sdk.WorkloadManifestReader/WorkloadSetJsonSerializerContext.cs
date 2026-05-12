// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Sdk.WorkloadManifestReader;

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    WriteIndented = true)]
[JsonSerializable(typeof(IDictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class WorkloadSetJsonSerializerContext : JsonSerializerContext;
