// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Provides source-generated JSON metadata for file-based application artifact metadata.
/// </summary>
[JsonSerializable(typeof(RunFileArtifactsMetadata))]
internal partial class RunFileJsonSerializerContext : JsonSerializerContext;
