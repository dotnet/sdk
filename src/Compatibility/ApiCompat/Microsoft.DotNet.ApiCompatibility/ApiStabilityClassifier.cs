// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility
{
    internal static class ApiStabilityClassifier
    {
        internal const string ExperimentalAttributeMetadataName = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";

        public static ApiStability Classify(ISymbol? symbol)
        {
            for (ISymbol? current = symbol; current != null; current = current.ContainingSymbol)
            {
                if (current.GetAttributes().Any(IsExperimentalAttribute))
                {
                    return ApiStability.Experimental;
                }
            }

            return ApiStability.Stable;
        }

        public static bool IsExperimentalAttribute(AttributeData attribute) =>
            attribute.AttributeClass?.ToDisplayString() == ExperimentalAttributeMetadataName;
    }
}
