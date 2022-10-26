// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface provides ordering for namespaces, types and members.
/// </summary>
public interface IAssemblySymbolOrderProvider
{
    /// <summary>
    /// Sorts the elements of a INamespaceSymbol.
    /// </summary>
    /// <param name="namespaces">List of namespaces to be sorted.</param>
    /// <returns>Returns namespaces in sorted order.</returns>
    IEnumerable<INamespaceSymbol> Order(IEnumerable<INamespaceSymbol> namespaces);

    /// <summary>
    /// Sorts the elements of a ITypeSymbol.
    /// </summary>
    /// <param name="namespaces">List of TypeMembers to be sorted.</param>
    /// <returns>Returns TypeMembers in sorted order.</returns>
    IEnumerable<T> Order<T>(IEnumerable<T> symbols) where T : ITypeSymbol;

    /// <summary>
    /// Sorts the elements of a ISymbol.
    /// </summary>
    /// <param name="namespaces">List of Members to be sorted.</param>
    /// <returns>Returns Members in sorted order.</returns>
    IEnumerable<ISymbol> Order(IEnumerable<ISymbol> members);
}
