// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.GenAPI;

/// <summary>
/// Options for running GenAPI via <see cref="GenAPIApp"/>.
/// </summary>
public sealed class GenAPIOptions
{
    public GenAPIOptions(string[] assembliesPaths)
    {
        AssembliesPaths = assembliesPaths ?? throw new ArgumentNullException(nameof(assembliesPaths));
    }

    /// <summary>
    /// The path to one or more assemblies or directories with assemblies.
    /// </summary>
    public string[] AssembliesPaths { get; }

    /// <summary>
    /// Paths to assembly references or their underlying directories.
    /// </summary>
    public string[]? AssemblyReferencesPaths { get; set; }

    /// <summary>
    /// Output path. Default is the console. Can specify an existing directory as well and
    /// then a file will be created for each assembly with the matching name of the assembly.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Specify a file with an alternate header content to prepend to output.
    /// </summary>
    public string? HeaderFile { get; set; }

    /// <summary>
    /// When set, method bodies will consist of <c>throw new PlatformNotSupportedException(ExceptionMessage);</c>.
    /// When <see cref="ExceptionMessage"/> is <see langword="null"/>, method bodies will instead be emitted as <c>throw null;</c>.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// The path to one or more api exclusion files with types in DocId format.
    /// </summary>
    public string[]? ExcludeApiFiles { get; set; }

    /// <summary>
    /// The path to one or more attribute exclusion files with types in DocId format.
    /// </summary>
    public string[]? ExcludeAttributesFiles { get; set; }

    /// <summary>
    /// If true, includes both internal and public API.
    /// </summary>
    public bool RespectInternals { get; set; }

    /// <summary>
    /// Includes assembly attributes which are values that provide information about an assembly.
    /// </summary>
    public bool IncludeAssemblyAttributes { get; set; }
}
