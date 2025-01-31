// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.GenAPI.Tests;

public class TestAssemblyLoaderFactory
{
    public static (IAssemblySymbolLoader, Dictionary<string, IAssemblySymbol>) CreateFromTexts(ILog log, (string, string)[] assemblyTexts, bool respectInternals = false, bool allowUnsafe = false)
    {
        if (assemblyTexts.Length == 0)
        {
            return (new AssemblySymbolLoader(log, resolveAssemblyReferences: true, includeInternalSymbols: respectInternals), new Dictionary<string, IAssemblySymbol>());
        }

        AssemblySymbolLoader loader = new(log, resolveAssemblyReferences: true, includeInternalSymbols: respectInternals);
        loader.AddReferenceSearchPaths(typeof(object).Assembly!.Location!);
        loader.AddReferenceSearchPaths(typeof(DynamicAttribute).Assembly!.Location!);

        Dictionary<string, IAssemblySymbol> assemblySymbols = new();
        foreach ((string assemblyName, string assemblyText) in assemblyTexts)
        {
            using Stream assemblyStream = SymbolFactory.EmitAssemblyStreamFromSyntax(assemblyText, enableNullable: true, allowUnsafe: allowUnsafe, assemblyName: assemblyName);
            if (loader.LoadAssembly(assemblyName, assemblyStream) is IAssemblySymbol assemblySymbol)
            {
                assemblySymbols.Add(assemblyName, assemblySymbol);
            }
        }

        return (loader, assemblySymbols);
    }
}
