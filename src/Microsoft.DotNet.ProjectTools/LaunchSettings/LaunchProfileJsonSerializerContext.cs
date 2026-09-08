// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.ProjectTools;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(ProjectLaunchProfile))]
[JsonSerializable(typeof(ExecutableLaunchProfile))]
internal partial class LaunchProfileJsonSerializerContext : JsonSerializerContext;
