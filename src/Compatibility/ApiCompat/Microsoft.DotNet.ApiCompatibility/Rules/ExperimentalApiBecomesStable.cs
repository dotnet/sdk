// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class ExperimentalApiBecomesStable : IRule
    {
        public ExperimentalApiBecomesStable(IRuleSettings settings, IRuleRegistrationContext context)
        {
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
        }

        private static void RunOnTypeSymbol(ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) =>
            AddDifference(left, right, leftMetadata, rightMetadata, differences);

        private static void RunOnMemberSymbol(ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) =>
            AddDifference(left, right, leftMetadata, rightMetadata, differences);

        internal static void AddDifference(ISymbol? left,
            ISymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is null || right is null ||
                ApiStabilityClassifier.Classify(left) != ApiStability.Experimental ||
                ApiStabilityClassifier.Classify(right) != ApiStability.Stable)
            {
                return;
            }

            differences.Add(new CompatDifference(
                leftMetadata,
                rightMetadata,
                DiagnosticIds.ExperimentalApiBecomesStable,
                string.Format(Resources.ExperimentalApiBecomesStable, right.ToDisplayString(SymbolExtensions.DisplayFormat)),
                DifferenceType.Changed,
                right.GetDocumentationCommentId(),
                DifferenceSeverity.Error));
        }
    }
}
