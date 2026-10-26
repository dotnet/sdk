// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class GlobalJsonContents
{
#pragma warning disable CA1034 // Do not nest type SdkSection
    public class SdkSection
    {
        public string? Version { get; set; }
        public bool? AllowPrerelease { get; set; }
        public string? RollForward { get; set; }
        public string[]? Paths { get; set; }
    }
#pragma warning restore CA1034

    public SdkSection? Sdk { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GlobalJsonContents))]
public partial class GlobalJsonContentsJsonContext : JsonSerializerContext
{
}
