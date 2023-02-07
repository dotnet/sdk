// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    internal static class INamedTypeSymbolExtension
    {
        public static bool HasIndexer(this INamedTypeSymbol type)
        {
            return type.GetMembers().Where(m => m is IPropertySymbol).Select(m => (IPropertySymbol)m).Any(m => m.IsIndexer);
        }
    }
}
