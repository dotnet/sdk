// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the parameter names between public methods do not change.
    /// </summary>
    public class CannotChangeParameterName : IRule
    {
        private readonly RuleSettings _settings;

        public CannotChangeParameterName(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
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

            for (int i = 0; i < leftMethod.Parameters.Length; i++)
            {
                IParameterSymbol leftParam = leftMethod.Parameters[i];
                IParameterSymbol rightParam = rightMethod.Parameters[i];

                if (!leftParam.Name.Equals(rightParam.Name))
                {
                    string msg = string.Format(Resources.CannotChangeParameterName, left, leftParam.Name, rightParam.Name);
                    string refId = $"{leftMethod.GetDocumentationCommentId()}${i}";
                    differences.Add(new CompatDifference(leftMetadata, rightMetadata, DiagnosticIds.CannotChangeParameterName, msg, DifferenceType.Changed, refId));
                }
            }
        }
    }
}
