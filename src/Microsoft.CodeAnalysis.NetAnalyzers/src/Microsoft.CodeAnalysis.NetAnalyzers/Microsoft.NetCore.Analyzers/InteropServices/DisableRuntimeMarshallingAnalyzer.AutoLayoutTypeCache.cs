// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    internal sealed partial class DisableRuntimeMarshallingAnalyzer
    {
        private sealed class AutoLayoutTypeCache
        {
            private readonly INamedTypeSymbol? _structLayoutAttribute;
            private readonly ConcurrentDictionary<ITypeSymbol, bool> _cache = new();

            public AutoLayoutTypeCache(Compilation compilation)
            {
                _structLayoutAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesStructLayoutAttribute);
            }

            public bool TypeIsAutoLayoutOrContainsAutoLayout(ITypeSymbol type)
            {
                if (_structLayoutAttribute is null)
                {
                    // We're in a scenario with a custom core library and we don't have any way to determine layout as the pseudo-attribute is not defined.
                    return false;
                }

                return TypeIsAutoLayoutOrContainsAutoLayout(type, ImmutableHashSet<ITypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default));

                bool TypeIsAutoLayoutOrContainsAutoLayout(ITypeSymbol type, ImmutableHashSet<ITypeSymbol> seenTypes)
                {
                    Debug.Assert(type.IsValueType);

                    if (_cache.TryGetValue(type, out bool isAutoLayoutOrContainsAutoLayout))
                    {
                        return isAutoLayoutOrContainsAutoLayout;
                    }

                    if (seenTypes.Contains(type.OriginalDefinition))
                    {
                        // If we have a recursive type, we are in one of two scenarios.
                        // 1. We're analyzing CoreLib and see the struct definition of a primitive type.
                        // In all of these cases, the type does not have auto layout.
                        // 2. We found a recursive type definition.
                        // Recursive type definitions are invalid and Roslyn will emit another error diagnostic anyway,
                        // so we don't care here.
                        _cache.TryAdd(type, false);
                        return false;
                    }

                    foreach (var attr in type.GetAttributes(_structLayoutAttribute))
                    {
                        if (attr.ConstructorArguments.Length > 0
                            && attr.ConstructorArguments[0] is TypedConstant argument
                            && argument.Type is not null)
                        {
                            SpecialType specialType = argument.Type.TypeKind == TypeKind.Enum ?
                                ((INamedTypeSymbol)argument.Type).EnumUnderlyingType!.SpecialType :
                                argument.Type.SpecialType;

                            if (DiagnosticHelpers.TryConvertToUInt64(argument.Value, specialType, out ulong convertedLayoutKindValue) &&
                                convertedLayoutKindValue == (ulong)LayoutKind.Auto)
                            {
                                _cache.TryAdd(type, true);
                                return true;
                            }
                        }
                    }

                    var seenTypesWithCurrentType = seenTypes.Add(type.OriginalDefinition);

                    foreach (var member in type.GetMembers())
                    {
                        if (member is IFieldSymbol { IsStatic: false, Type.IsValueType: true } valueTypeField
                            && TypeIsAutoLayoutOrContainsAutoLayout(valueTypeField.Type, seenTypesWithCurrentType))
                        {
                            _cache.TryAdd(type, true);
                            return true;
                        }
                    }

                    _cache.TryAdd(type, false);
                    return false;
                }
            }
        }
    }
}
