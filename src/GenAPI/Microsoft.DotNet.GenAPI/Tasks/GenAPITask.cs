// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks;
using Microsoft.DotNet.GenAPI.Shared;

namespace Microsoft.DotNet.GenAPI.Tasks;

#nullable enable

/// <summary>
/// MSBuild task frontend for the Roslyn-based GenAPI.
/// </summary>
public class GenAPITask : BuildTask
{
    /// <summary>
    /// Delimited (',' or ';') set of paths for assemblies or directories to get all assemblies.
    /// </summary>
    [Required]
    public string? Assembly { get; set; }

    /// <summary>
    /// If true, tries to resolve assembly reference.
    /// </summary>
    public bool? ResolveAssemblyReferences { get; set; }
    
    /// <summary>
    /// Delimited (',' or ';') set of paths to use for resolving assembly references.
    /// </summary>
    public string? LibPath { get; set; }

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
    /// Method bodies should throw PlatformNotSupportedException.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Specify a path to a file with the list in the DocId format of which attributes should be excluded from being applied on apis.
    /// </summary>
    public string? ExcludeAttributesList { get; set; }

    public override bool Execute()
    {
        GenAPIApp.Run(new GenAPIApp.Context
        {
            Assembly = Assembly!,
            ResolveAssemblyReferences = ResolveAssemblyReferences,
            LibPath = LibPath,
            OutputPath = OutputPath,
            HeaderFile = HeaderFile,
            ExceptionMessage = ExceptionMessage,
            ExcludeAttributesList = ExcludeAttributesList,
        });

        return true;
    }
}
