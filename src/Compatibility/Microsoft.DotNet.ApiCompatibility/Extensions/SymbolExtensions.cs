﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Extensions
{
    internal static class SymbolExtensions
    {
        private static SymbolDisplayFormat Format { get; } = GetSymbolDisplayFormat();

        private static SymbolDisplayFormat GetSymbolDisplayFormat()
        {
            // This is the default format for symbol.ToDisplayString;
            SymbolDisplayFormat format = SymbolDisplayFormat.CSharpErrorMessageFormat;

            // Remove ? annotations from reference types as we want to map the APIs without nullable annotations
            // and have a special rule to catch those differences.
            format = format.WithMiscellaneousOptions(format.MiscellaneousOptions &
                ~SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName &
                ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

            // Remove ref/out from parameters to compare APIs when building the mappers.
            return format.WithParameterOptions(format.ParameterOptions & ~SymbolDisplayParameterOptions.IncludeParamsRefOut);
        }

        internal static string ToComparisonDisplayString(this ISymbol symbol) => symbol.ToDisplayString(Format);

        internal static IEnumerable<ITypeSymbol> GetAllBaseTypes(this ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Interface)
            {
                foreach (ITypeSymbol @interface in type.Interfaces)
                {
                    yield return @interface;
                    foreach (ITypeSymbol baseInterface in @interface.GetAllBaseTypes())
                        yield return baseInterface;
                }
            }
            else if (type.BaseType != null)
            {
                yield return type.BaseType;
                foreach (ITypeSymbol baseType in type.BaseType.GetAllBaseTypes())
                    yield return baseType;
            }
        }
    }
}
