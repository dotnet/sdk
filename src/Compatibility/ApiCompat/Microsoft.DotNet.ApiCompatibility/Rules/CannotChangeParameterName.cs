// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the parameter names between public methods do not change.
    /// </summary>
    public class CannotChangeParameterName : IRule
    {
        public CannotChangeParameterName(IRuleSettings settings, IRuleRegistrationContext context)
        {
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
        }


        private void RunOnTypeSymbol(ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is not INamedTypeSymbol leftType || right is not INamedTypeSymbol rightType)
            {
                return;
            }

            CompareParameters(leftType.TypeParameters, rightType.TypeParameters, left, leftMetadata, rightMetadata, differences, parameterIndicator: "``");
        }

        private void RunOnMemberSymbol(
            ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is not IMethodSymbol leftMethod || right is not IMethodSymbol rightMethod)
            {
                return;
            }

            CompareParameters(leftMethod.Parameters, rightMethod.Parameters, left, leftMetadata, rightMetadata, differences);

            CompareParameters(leftMethod.TypeParameters, rightMethod.TypeParameters, left, leftMetadata, rightMetadata, differences, parameterIndicator: "``");

        }

        private void CompareParameters(IReadOnlyList<ISymbol> leftParameters,
            IReadOnlyList<ISymbol> rightParameters,
            ISymbol left,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences,
            string parameterIndicator = "$")
        {
            Debug.Assert(leftParameters.Count == rightParameters.Count);

            for (int i = 0; i < leftParameters.Count; i++)
            {
                ISymbol leftParam = leftParameters[i];
                ISymbol rightParam = rightParameters[i];

                if (!leftParam.Name.Equals(rightParam.Name))
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotChangeParameterName,
                        string.Format(Resources.CannotChangeParameterName, left, leftParam.Name, rightParam.Name),
                        DifferenceType.Changed,
                        $"{left.GetDocumentationCommentId()}{parameterIndicator}{i}"));
                }
            }
        }
    }
}
