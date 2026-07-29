// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Represents the persisted build and launch state for a file-based application.
/// </summary>
internal sealed class RunFileBuildCacheEntry
{
    private static StringComparer GlobalPropertiesComparer => StringComparer.OrdinalIgnoreCase;

    private static StringComparer FilePathComparer => StringComparer.Ordinal;

    /// <summary>Gets the global properties used for the build.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, string> GlobalProperties { get; }

    /// <summary>Gets the full paths of implicit build inputs.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HashSet<string> ImplicitBuildFiles { get; }

    /// <summary>Gets or sets file directives recognized by the SDK, excluding the shebang.</summary>
    public ImmutableArray<string> Directives { get; set; } = [];

    /// <summary>Gets the full paths of non-entry-point files that participate in the build.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HashSet<string> AdditionalSources { get; }

    /// <summary>Gets or sets the build level used to produce this entry.</summary>
    public BuildLevel BuildLevel { get; set; }

    /// <summary>Gets or sets the SDK version used to produce this entry.</summary>
    public string? SdkVersion { get; set; }

    /// <summary>Gets or sets the runtime version used to produce this entry.</summary>
    public string? RuntimeVersion { get; set; }

    /// <summary>Gets or sets the cached launch properties.</summary>
    public RunProperties? Run { get; set; }

    /// <summary>Gets or sets arguments captured from the C# compiler invocation.</summary>
    public ImmutableArray<string> CscArguments { get; set; } = [];

    /// <summary>Gets or sets the final compiler output copied from the intermediate directory.</summary>
    public string? BuildResultFile { get; set; }

    /// <summary>
    /// Initializes an empty cache entry for JSON deserialization.
    /// </summary>
    [JsonConstructor]
    public RunFileBuildCacheEntry()
    {
        GlobalProperties = new(GlobalPropertiesComparer);
        ImplicitBuildFiles = new(FilePathComparer);
        AdditionalSources = new(FilePathComparer);
    }

    /// <summary>
    /// Initializes a cache entry with the effective global properties.
    /// </summary>
    /// <param name="globalProperties">The effective global properties with an ordinal-ignore-case comparer.</param>
    public RunFileBuildCacheEntry(Dictionary<string, string> globalProperties)
    {
        Debug.Assert(globalProperties.Comparer == GlobalPropertiesComparer);
        GlobalProperties = globalProperties;
        ImplicitBuildFiles = new(FilePathComparer);
        AdditionalSources = new(FilePathComparer);
    }
}
