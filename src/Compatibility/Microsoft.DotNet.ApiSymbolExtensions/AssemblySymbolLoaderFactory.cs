// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Factory to create an AssemblySymbolLoader
    /// </summary>
    /// <param name="includeInternalSymbols">True to include internal API when reading assemblies from the <see cref="AssemblySymbolLoader"/> created.</param>
    public sealed class AssemblySymbolLoaderFactory(bool includeInternalSymbols = false) : IAssemblySymbolLoaderFactory
    {
        /// <inheritdoc />
        public IAssemblySymbolLoader Create(bool shouldResolveReferences) =>
            new AssemblySymbolLoader(shouldResolveReferences, includeInternalSymbols);
    }
}
