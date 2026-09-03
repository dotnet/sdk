// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Provides source-generated JSON metadata for file-based application cache contracts.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(RunFileBuildCacheEntry))]
internal partial class RunFileBuildCacheJsonSerializerContext : JsonSerializerContext;
