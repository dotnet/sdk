// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility
{
    internal static class ApiStabilityClassifier
    {
        internal const string ExperimentalAttributeMetadataName = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";

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
                Classify(symbol.ContainingSymbol) == ApiStability.Experimental
                    ? ApiStability.Experimental
                    : ApiStability.Stable;

            return s_classifications.GetValue(symbol, _ => new(stability)).Stability;
        }

        public static bool IsExperimentalAttribute(AttributeData attribute) =>
            attribute.AttributeClass?.ToDisplayString() == ExperimentalAttributeMetadataName;

        private sealed class CacheEntry(ApiStability stability)
        {
            public ApiStability Stability { get; } = stability;
        }
    }
}
