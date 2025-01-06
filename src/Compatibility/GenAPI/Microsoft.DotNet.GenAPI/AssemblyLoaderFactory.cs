// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.GenAPI.Filtering;

namespace Microsoft.DotNet.GenAPI;

/// <summary>
/// Class that facilitates the creation of an assembly symbol loader and its corresponding assembly symbols.
/// </summary>
public class AssemblyLoaderFactory
{
    /// <summary>
    /// Creates an assembly symbol loader and its corresponding assembly symbols from the given DLL files in the filesystem.
    /// </summary>
    /// <param name="assembliesPaths">A collection of paths where the assembly DLLs should be searched.</param>
    /// <param name="assemblyReferencesPaths">An optional collection of paths where the assembly references should be searched.</param>
    /// <param name="respectInternals">Whether to include internal symbols or not.</param>
    /// <returns>A tuple containing the assembly symbol loader and a dictionary of assembly symbols.</returns>
    public static (IAssemblySymbolLoader, Dictionary<string, IAssemblySymbol>) CreateFromFiles(string[] assembliesPaths, string[]? assemblyReferencesPaths, bool respectInternals = false)
    {
        AssemblySymbolLoader loader;
        Dictionary<string, IAssemblySymbol> assemblySymbols;

        if (assembliesPaths.Length == 0)
        {
            CreateWithNoAssemblies();
        }

        bool atLeastOneReferencePath = assemblyReferencesPaths?.Count() > 0;
        loader = new AssemblySymbolLoader(resolveAssemblyReferences: atLeastOneReferencePath, respectInternals);
        if (atLeastOneReferencePath)
        {
            loader.AddReferenceSearchPaths(assemblyReferencesPaths!);
        }
        assemblySymbols = new Dictionary<string, IAssemblySymbol>(loader.LoadAssembliesAsDictionary(assembliesPaths));

        return (loader, assemblySymbols);
    }

    /// <summary>
    /// Creates a default assembly symbol loader and a dictionary of assembly symbols.
    /// </summary>
    /// <param name="respectInternals">Whether to include internal symbols or not.</param>
    /// <returns>A tuple containing the assembly symbol loader and a dictionary of assembly symbols.</returns>
    public static (IAssemblySymbolLoader, Dictionary<string, IAssemblySymbol>) CreateWithNoAssemblies(bool respectInternals = false) =>
        (new AssemblySymbolLoader(resolveAssemblyReferences: true, includeInternalSymbols: respectInternals), new Dictionary<string, IAssemblySymbol>());
}
