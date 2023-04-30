// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Interface encapsulates logic for processing symbol assemblies.
    /// </summary>
    public interface IAssemblySymbolWriter
    {
        /// <summary>
        /// Process a given assembly symbol.
        /// </summary>
        /// <param name="assemblySymbol"><see cref="IAssemblySymbol"/> representing the loaded assembly.</param>
        void WriteAssembly(IAssemblySymbol assemblySymbol);
    }
}
