// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Interface encapsulates logic for processing symbol assemblies.
    /// </summary>
    public interface IAssemblySymbolWriter
    {
        /// <summary>
        /// Write a given assembly symbol to the instance's desired output.
        /// </summary>
        /// <param name="assemblySymbol">An assembly symbol representing the loaded assembly.</param>
        void WriteAssembly(IAssemblySymbol assemblySymbol);

        /// <summary>
        /// Returns the configured source code document for the specified assembly symbol.
        /// </summary>
        /// <param name="assemblySymbol">The assembly symbol that represents the loaded assembly.</param>
        /// <returns>The source code document instance of the specified assembly symbol.</returns>
        Document GetDocumentForAssembly(IAssemblySymbol assemblySymbol);

        /// <summary>
        /// Returns the formatted root syntax node for the specified document.
        /// </summary>
        /// <param name="document">A source code document instance.</param>
        /// <returns>The root syntax node of the specified document.</returns>
        public SyntaxNode GetFormattedRootNodeForDocument(Document document);
    }
}
