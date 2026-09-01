// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.DotNet.FileBasedPrograms;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Represents the persisted build and launch state for a file-based application.
/// </summary>
internal sealed class RunFileBuildCacheEntry
{
    private static StringComparer GlobalPropertiesComparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// We can't know which parts of the path are case insensitive, so we are conservative
    /// to avoid false positives in the cache (saying we are up to date even if we are not).
    /// </summary>
    private static StringComparer FilePathComparer => StringComparer.Ordinal;

    /// <summary>If the user-provided entry point file path is a symlink, this is the link target.</summary>
    /// <remarks>Should be required and init-only but https://github.com/dotnet/runtime/issues/92877.</remarks>
    public string? AliasedEntryPointFilePath { get; set; }

    /// <summary>Gets the global properties used for the build.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, string> GlobalProperties { get; }

    /// <summary>Gets the full paths of implicit build inputs.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HashSet<string> ImplicitBuildFiles { get; }

    /// <summary>
    /// <see cref="CSharpDirective"/>s from the entry point file recognized by the SDK (i.e., except shebang).
    /// </summary>
    public ImmutableArray<string> Directives { get; set; } = [];

    /// <summary>
    /// Full paths of non-entry-point files that participate in the build
    /// (e.g., default items like <c>.resx</c> and C# source files from <c>#:include</c> directives).
    /// </summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public HashSet<string> AdditionalSources { get; }

    /// <summary>Gets or sets the build level used to produce this entry.</summary>
    public BuildLevel BuildLevel { get; set; }

    /// <summary>Gets or sets the SDK version used to produce this entry.</summary>
    /// <remarks>Should be required and init-only but https://github.com/dotnet/runtime/issues/92877.</remarks>
    public string? SdkVersion { get; set; }

    /// <summary>Gets or sets the runtime version used to produce this entry.</summary>
    /// <remarks>Should be required and init-only but https://github.com/dotnet/runtime/issues/92877.</remarks>
    public string? RuntimeVersion { get; set; }

    /// <summary>Gets or sets the cached launch properties.</summary>
    public RunProperties? Run { get; set; }

    /// <summary>
    /// <see cref="CSharpCompilerCommand.CscArguments"/>
    /// </summary>
    public ImmutableArray<string> CscArguments { get; set; } = [];

    /// <summary>
    /// <see cref="CSharpCompilerCommand.BuildResultFile"/>
    /// </summary>
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
