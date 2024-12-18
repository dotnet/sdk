// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.GenAPI.Filtering;

namespace Microsoft.DotNet.GenAPI;

public class AssemblyLoaderFactory
{
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

    public static (IAssemblySymbolLoader, Dictionary<string, IAssemblySymbol>) CreateWithNoAssemblies(bool respectInternals = false) =>
        (new AssemblySymbolLoader(resolveAssemblyReferences: true, includeInternalSymbols: respectInternals), new Dictionary<string, IAssemblySymbol>());
}
