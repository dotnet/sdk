// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class CannotReOrderStructPublicProperties : IRule
    {
        public CannotReOrderStructPublicProperties(IRuleRegistrationContext context)
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
                    var leftProperties = left.GetMembers().Where(m => m.Kind == SymbolKind.Property && m.DeclaredAccessibility == Accessibility.Public).ToList();
                    var rightProperties = right.GetMembers().Where(m => m.Kind == SymbolKind.Property && m.DeclaredAccessibility == Accessibility.Public).ToList();

                    // check if left and right both have same number of public properties
                    if (leftProperties.Count == rightProperties.Count)
                    {
                        // check if left and right both have same public properties in same order
                        bool sameOrder = true;
                        for (int i = 0; i < leftProperties.Count; i++)
                        {
                            if (leftProperties[i].Name != rightProperties[i].Name)
                            {
                                sameOrder = false;
                                break;
                            }
                        }

                        if (!sameOrder)
                        {
                            //differences.Add(new CompatDifference(leftMetadata,
                          //rightMetadata,DiagnosticIds.CannotReOrderStructPublicProperties,string.Format(Resources.CannotReOrderStructPublicProperties, right.ToDisplayString(SymbolExtensions.DisplayFormat), rightMetadata, leftMetadata),
                         //DifferenceType.Changed,right));
                        }
                    }
                }
            }
        }
    }
}
