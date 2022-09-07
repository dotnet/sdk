﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System;
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
            // Also don't use keyword names for special types. This makes the comparison more accurate when no
            // references are running or if one side has references and the other doesn't.
            format = format.WithMiscellaneousOptions(format.MiscellaneousOptions &
                ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes &
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

        internal static bool IsEffectivelySealed(this ITypeSymbol type, bool includeInternals) =>
            type.IsSealed || !HasVisibleConstructor(type, includeInternals);

        private static bool HasVisibleConstructor(ITypeSymbol type, bool includeInternals)
        {
            if (type is INamedTypeSymbol namedType)
            {
                foreach (IMethodSymbol constructor in namedType.Constructors)
                {
                    if (!constructor.IsStatic && constructor.IsVisibleOutsideOfAssembly(includeInternals))
                        return true;
                }
            }

            return false;
        }

        internal static IEnumerable<ITypeSymbol> GetAllBaseInterfaces(this ITypeSymbol type)
        {
            foreach (ITypeSymbol @interface in type.Interfaces)
            {
                yield return @interface;
                foreach (ITypeSymbol baseInterface in @interface.GetAllBaseInterfaces())
                    yield return baseInterface;
            }

            foreach (ITypeSymbol baseType in type.GetAllBaseTypes())
                foreach (ITypeSymbol baseInterface in baseType.GetAllBaseInterfaces())
                    yield return baseInterface;
        }

        internal static bool IsVisibleOutsideOfAssembly(this ISymbol symbol, bool includeInternals) =>
            symbol.DeclaredAccessibility == Accessibility.Public ||
            symbol.DeclaredAccessibility == Accessibility.Protected ||
            symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal ||
            (includeInternals && symbol.DeclaredAccessibility != Accessibility.Private);

        internal static bool IsEventAdderOrRemover(this IMethodSymbol method) =>
            method.MethodKind == MethodKind.EventAdd ||
            method.MethodKind == MethodKind.EventRemove ||
            method.Name.StartsWith("add_", StringComparison.Ordinal) ||
            method.Name.StartsWith("remove_", StringComparison.Ordinal);
    }
}
