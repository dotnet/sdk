// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface responsible for filtering attributes, namespaces, types and members.
/// </summary>
public interface IAssemblySymbolFilter
{
    /// <summary>
    /// Including/fitlering out namespace symbol and it's types, members.
    /// </summary>
    /// <param name="ns">Object of <cref="INamespaceSymbol"/>.</param>
    /// <returns>Returns boolean value.</returns>
    bool Includes(INamespaceSymbol ns);

    /// <summary>
    /// Including/fitlering out attribute data.
    /// </summary>
    /// <param name="ns">Object of <cref="AttributeData"/>.</param>
    /// <returns>Returns boolean value.</returns>
    bool Includes(AttributeData at);

    /// <summary>
    /// Including/fitlering out type symbol and it's members .
    /// </summary>
    /// <param name="ns">Object of <cref="ITypeSymbol"/>.</param>
    /// <returns>Returns boolean value.</returns>
    bool Includes(ITypeSymbol ts);

    /// <summary>
    /// Including/fitlering out member symbol.
    /// </summary>
    /// <param name="ns">Object of <cref="ISymbol"/>.</param>
    /// <returns>Returns boolean value.</returns>
    bool Includes(ISymbol member);
}
