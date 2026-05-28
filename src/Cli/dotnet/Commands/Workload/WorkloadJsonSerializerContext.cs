// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Commands.Workload;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WorkloadHistoryRecord))]
internal partial class WorkloadHistoryJsonSerializerContext : JsonSerializerContext;

/// <summary>
/// Local converter for <see cref="WorkloadPackId"/> so the source generator can access it
/// (the original converter in WorkloadManifestReader is internal).
/// </summary>
internal sealed class WorkloadPackIdJsonConverter : JsonConverter<WorkloadPackId>
{
    public override WorkloadPackId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, WorkloadPackId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

[JsonSourceGenerationOptions(Converters = [typeof(WorkloadPackIdJsonConverter)])]
[JsonSerializable(typeof(PackInfo))]
internal partial class PackInfoJsonSerializerContext : JsonSerializerContext;
