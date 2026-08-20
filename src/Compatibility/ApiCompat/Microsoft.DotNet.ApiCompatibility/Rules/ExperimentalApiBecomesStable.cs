// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class ExperimentalApiBecomesStable : IRule
    {
        private readonly IRuleSettings _settings;

        public ExperimentalApiBecomesStable(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) =>
            AddDifference(left, right, leftMetadata, rightMetadata, _settings.AttributeDataSymbolFilter, differences);

        private void RunOnMemberSymbol(ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) =>
            AddDifference(left, right, leftMetadata, rightMetadata, _settings.AttributeDataSymbolFilter, differences);

        internal static void AddDifference(ISymbol? left,
            ISymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            ISymbolFilter attributeDataSymbolFilter,
            IList<CompatDifference> differences)
        {
            if (left is null || right is null)
            {
                return;
            }

            ApiStability leftStability = ApiStabilityClassifier.Classify(left, attributeDataSymbolFilter);
            ApiStability rightStability = ApiStabilityClassifier.Classify(right, attributeDataSymbolFilter);
            (string diagnosticId, string message) = (leftStability, rightStability) switch
            {
                (ApiStability.Experimental, ApiStability.Stable) => (
                    DiagnosticIds.ExperimentalApiBecomesStable,
                    string.Format(Resources.ExperimentalApiBecomesStable, right.ToDisplayString(SymbolExtensions.DisplayFormat))),
                (ApiStability.Stable, ApiStability.Experimental) => (
                    DiagnosticIds.StableApiBecomesExperimental,
                    string.Format(Resources.StableApiBecomesExperimental, right.ToDisplayString(SymbolExtensions.DisplayFormat))),
                _ => default,
            };

            if (diagnosticId is null)
            {
                return;
            }

            differences.Add(new CompatDifference(
                leftMetadata,
                rightMetadata,
                diagnosticId,
                message,
                DifferenceType.Changed,
                right.GetDocumentationCommentId(),
                DifferenceSeverity.Error));
        }
    }
}
