// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility
{
    internal static class ApiStabilityClassifier
    {
        private const string ExperimentalAttributeName = "ExperimentalAttribute";

        private static readonly ConditionalWeakTable<ISymbol, CacheEntry> s_classifications = new();

        public static ApiStability Classify(ISymbol? symbol)
        {
            if (symbol is null)
            {
                return ApiStability.Stable;
            }

            if (s_classifications.TryGetValue(symbol, out CacheEntry? cached))
            {
                return cached.Stability;
            }

            ApiStability stability = symbol.GetAttributes().Any(IsExperimentalAttribute) ||
                symbol is IMethodSymbol { AssociatedSymbol: { } associatedSymbol } &&
                    associatedSymbol.GetAttributes().Any(IsExperimentalAttribute) ||
                symbol is not IModuleSymbol &&
                    symbol.ContainingModule?.GetAttributes().Any(IsExperimentalAttribute) == true ||
                symbol is not IAssemblySymbol &&
                    symbol.ContainingAssembly?.GetAttributes().Any(IsExperimentalAttribute) == true
                    ? ApiStability.Experimental
                    : ApiStability.Stable;

            return s_classifications.GetValue(symbol, _ => new(stability)).Stability;
        }

        public static bool IsExperimentalAttribute(AttributeData attribute) =>
            attribute.AttributeClass is
            {
                MetadataName: ExperimentalAttributeName,
                ContainingNamespace:
                {
                    Name: "CodeAnalysis",
                    ContainingNamespace:
                    {
                        Name: "Diagnostics",
                        ContainingNamespace:
                        {
                            Name: "System",
                            ContainingNamespace.IsGlobalNamespace: true
                        }
                    }
                }
            };

        private sealed class CacheEntry(ApiStability stability)
        {
            public ApiStability Stability { get; } = stability;
        }
    }
}
