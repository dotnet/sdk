// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the visibility of symbols is not reduced.
    /// In strict mode, it also checks that the visibility isn't expanded.
    /// </summary>
    public class CannotChangeVisibility : IRule
    {
        private readonly RuleSettings _settings;

        public CannotChangeVisibility(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(
            ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is null && right is null)
            {
                return;
            }

            Accessibility leftAccess = left?.DeclaredAccessibility ?? Accessibility.Private;
            Accessibility rightAccess = right?.DeclaredAccessibility ?? Accessibility.Private;

            if (leftAccess > rightAccess)
            {
                string msg = string.Format(Resources.CannotChangeVisibility, left, leftAccess, rightAccess);
                differences.Add(new CompatDifference(leftMetadata, rightMetadata, DiagnosticIds.CannotReduceVisibility, msg, DifferenceType.Changed, left!));
            }
            else if (_settings.StrictMode && rightAccess > leftAccess)
            {
                string msg = string.Format(Resources.CannotChangeVisibility, right, rightAccess, leftAccess);
                differences.Add(new CompatDifference(leftMetadata, rightMetadata, DiagnosticIds.CannotExpandVisibility, msg, DifferenceType.Changed, right!));
            }
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
            if (left is null && right is null)
            {
                return;
            }

            Accessibility leftAccess = left?.DeclaredAccessibility ?? Accessibility.Private;
            Accessibility rightAccess = right?.DeclaredAccessibility ?? Accessibility.Private;

            if (leftAccess > rightAccess)
            {
                if (leftAccess == Accessibility.Protected && leftContainingType.IsEffectivelySealed(_settings.IncludeInternalSymbols))
                {
                    return;
                }

                string msg = string.Format(Resources.CannotChangeVisibility, left, leftAccess, rightAccess);
                differences.Add(new CompatDifference(leftMetadata, rightMetadata, DiagnosticIds.CannotReduceVisibility, msg, DifferenceType.Changed, left!));
            }
            else if (_settings.StrictMode && rightAccess > leftAccess)
            {
                if (rightAccess == Accessibility.Protected && rightContainingType.IsEffectivelySealed(_settings.IncludeInternalSymbols))
                {
                    return;
                }

                string msg = string.Format(Resources.CannotChangeVisibility, right, rightAccess, leftAccess);
                differences.Add(new CompatDifference(leftMetadata, rightMetadata, DiagnosticIds.CannotExpandVisibility, msg, DifferenceType.Changed, right!));
            }
        }
    }
}
