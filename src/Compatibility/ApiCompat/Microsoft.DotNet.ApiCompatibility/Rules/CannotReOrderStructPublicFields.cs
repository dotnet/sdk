// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This rule validates that order of struct fields is maintained.
    /// </summary>
    public class CannotReOrderStructPublicFields : IRule
    {
        public CannotReOrderStructPublicFields(IRuleRegistrationContext context)
        {
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            // check if left and right both are struct
            if (left != null && right != null && left.TypeKind == TypeKind.Struct && right.TypeKind == TypeKind.Struct)
            {
                // check if left and right both are public
                if (left.DeclaredAccessibility == Accessibility.Public && right.DeclaredAccessibility == Accessibility.Public)
                {
                    var leftFields = left.GetMembers().Where(m => m.Kind == SymbolKind.Field && m.DeclaredAccessibility == Accessibility.Public).ToList();
                    var rightFields = right.GetMembers().Where(m => m.Kind == SymbolKind.Field && m.DeclaredAccessibility == Accessibility.Public).ToList();

                    // check if left and right both have same number of public properties
                    if (leftFields.Count == rightFields.Count)
                    {
                        // check if left and right both have same public properties in same order
                        bool sameOrder = true;
                        for (int i = 0; i < leftFields.Count; i++)
                        {
                            if (leftFields[i].Name != rightFields[i].Name)
                            {
                                sameOrder = false;
                                break;
                            }
                        }

                        if (!sameOrder)
                        {
                            differences.Add(new CompatDifference(
                                leftMetadata,
                                rightMetadata,
                                DiagnosticIds.CannotReOrderStructPublicFields,
                                string.Format(Resources.CannotReOrderStructPublicFields, right.ToDisplayString(SymbolExtensions.DisplayFormat)),
                            DifferenceType.Changed, right));
                        }
                    }
                }
            }
        }
    }
}
