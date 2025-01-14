// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Factory to create an AssemblySymbolLoader
    /// </summary>
    /// <param name="log">A logger instance used for logging messages.</param>
    /// <param name="includeInternalSymbols">True to include internal API when reading assemblies from the <see cref="AssemblySymbolLoader"/> created.</param>
    public sealed class AssemblySymbolLoaderFactory(ILog log, bool includeInternalSymbols = false) : IAssemblySymbolLoaderFactory
    {
        /// <inheritdoc />
        public IAssemblySymbolLoader Create(bool shouldResolveReferences) =>
            new AssemblySymbolLoader(log, shouldResolveReferences, includeInternalSymbols);
    }
}
