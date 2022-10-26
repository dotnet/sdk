// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public class AssemblySymbolOrderProvider : IAssemblySymbolOrderProvider
{
    /// <inheritdoc />
    public IEnumerable<INamespaceSymbol> Order(IEnumerable<INamespaceSymbol> namespaces)
    {
        return namespaces.OrderBy(n => n.ToDisplayString());
    }

    /// <inheritdoc />
    public IEnumerable<T> Order<T>(IEnumerable<T> symbols) where T : ITypeSymbol
    {
        return symbols.OrderBy(t => (t.DeclaredAccessibility != Accessibility.Public, t.Name));
    }

    /// <inheritdoc />
    public IEnumerable<ISymbol> Order(IEnumerable<ISymbol> members)
    {
        return members.OrderBy(t => (GetMemberOrder(t), t.DeclaredAccessibility != Accessibility.Public, t.Name));
    }

    private static int GetMemberOrder(ISymbol symbol)
    {
        return symbol switch
        {
            IFieldSymbol fieldSymbol when fieldSymbol.ContainingType.TypeKind == TypeKind.Enum
                => (int)Convert.ToInt64(fieldSymbol.ConstantValue),
            IMethodSymbol methodSymbol when methodSymbol.MethodKind == MethodKind.Constructor => -4,
            IFieldSymbol _ => -3,
            IPropertySymbol _ => -2,
            IMethodSymbol methodSymbol when methodSymbol.IsStatic => -1,
            _ => 0
        };
    }
}
