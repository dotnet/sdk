// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the attributes between public members do not change.
    /// </summary>
    public class AttributesMustMatch : IRule
    {
        private readonly IRuleSettings _settings;

        public AttributesMustMatch(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void AddDifference(IList<CompatDifference> differences,
            DifferenceType dt,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            ISymbol containing,
            string itemRef,
            AttributeData attributeData)
        {
            if (!_settings.StrictMode && dt == DifferenceType.Added)
            {
                return;
            }

            string? docId = attributeData.AttributeClass?.GetDocumentationCommentId();
            CompatDifference difference = dt switch
            {
                DifferenceType.Changed => new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotChangeAttribute,
                    string.Format(Resources.CannotChangeAttribute, attributeData.AttributeClass, containing),
                    DifferenceType.Changed,
                    $"{itemRef}:[{docId}]"),
                DifferenceType.Added => new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotAddAttribute,
                    string.Format(Resources.CannotAddAttribute, attributeData, containing),
                    DifferenceType.Added,
                    $"{itemRef}:[{docId}]"),
                DifferenceType.Removed => new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotRemoveAttribute,
                    string.Format(Resources.CannotRemoveAttribute, attributeData, containing),
                    DifferenceType.Removed,
                    $"{itemRef}:[{docId}]"),
                _ => throw new InvalidOperationException($"Unreachable DifferenceType '{dt}' encountered."),
            };

            differences.Add(difference);
        }

        private void ReportAttributeDifferences(ISymbol containing,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            string itemRef,
            IList<AttributeData> left,
            IList<AttributeData> right,
            IList<CompatDifference> differences)
        {
            // No attributes, nothing to do. Exit early.
            if (left.Count == 0 && right.Count == 0)
            {
                return;
            }

            // Build a set of attributes for both sides, grouped by their names.
            // For example,
            //   [Foo("a")]
            //   [Foo("b")]
            //   [Bar]
            //   public void F() {}
            // would give you a set like
            //   { { Foo("a"), Foo("b") }, { Bar } }
            AttributeSet leftAttributeSet = new(_settings, left);
            AttributeSet rightAttributeSet = new(_settings, right);

            foreach (AttributeGroup leftGroup in leftAttributeSet)
            {
                if (rightAttributeSet.TryGetValue(leftGroup.Representative, out AttributeGroup? rightGroup))
                {
                    // If attribute exists on left and the right, compare their arguments.
                    foreach (AttributeData leftAttribute in leftGroup.Attributes)
                    {
                        bool seen = false;
                        for (int j = 0; j < rightGroup.Attributes.Count; j++)
                        {
                            AttributeData rightAttribute = rightGroup.Attributes[j];
                            if (_settings.AttributeDataEqualityComparer.Equals(leftAttribute, rightAttribute))
                            {
                                rightGroup.Seen[j] = true;
                                seen = true;
                                break;
                            }
                        }

                        if (!seen)
                        {
                            // Attribute arguments exist on left but not right.
                            // Issue "changed" diagnostic.
                            AddDifference(differences, DifferenceType.Changed, leftMetadata, rightMetadata, containing, itemRef, leftAttribute);
                        }
                    }

                    for (int i = 0; i < rightGroup.Attributes.Count; i++)
                    {
                        if (!rightGroup.Seen[i] && _settings.StrictMode)
                        {
                            // Attribute arguments exist on right but not left.
                            // Left
                            //   [Foo("a")]
                            //   void F()
                            // Right
                            //   [Foo("a")]
                            //   [Foo("b")]
                            //   void F()
                            // Issue "changed" diagnostic when in strict mode.
                            AddDifference(differences, DifferenceType.Changed, leftMetadata, rightMetadata, containing, itemRef, rightGroup.Attributes[i]);
                        }
                    }
                }
                else
                {
                    // Attribute exists on left but not on right.
                    // Loop over left and issue "removed" diagnostic for each one.
                    foreach (AttributeData leftAttribute in leftGroup.Attributes)
                    {
                        AddDifference(differences, DifferenceType.Removed, leftMetadata, rightMetadata, containing, itemRef, leftAttribute);
                    }
                }
            }

            foreach (AttributeGroup rightGroup in rightAttributeSet)
            {
                if (leftAttributeSet.TryGetValue(rightGroup.Representative, out _))
                {
                    continue;
                }

                // Attribute exists on right but not left.
                // Loop over right and issue "added" diagnostic for each one.
                foreach (AttributeData rightAttribute in rightGroup.Attributes)
                {
                    AddDifference(differences, DifferenceType.Added, leftMetadata, rightMetadata, containing, itemRef, rightAttribute);
                }
            }
        }

        private void RunOnTypeSymbol(ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is null || right is null)
            {
                return;
            }

            // Compare type parameter attributes.
            if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed)
            {
                if (leftNamed.TypeParameters.Length == rightNamed.TypeParameters.Length)
                {
                    for (int i = 0; i < leftNamed.TypeParameters.Length; i++)
                    {
                        ReportAttributeDifferences(left,
                            leftMetadata,
                            rightMetadata,
                            left.GetDocumentationCommentId() + $"<{i}>",
                            leftNamed.TypeParameters[i].GetAttributes(),
                            rightNamed.TypeParameters[i].GetAttributes(),
                            differences);
                    }
                }
            }

            ReportAttributeDifferences(left,
                leftMetadata,
                rightMetadata,
                left.GetDocumentationCommentId() ?? "",
                left.GetAttributes(),
                right.GetAttributes(),
                differences);
        }

        private void RunOnMemberSymbol(ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is null || right is null)
            {
                return;
            }

            if (left is IMethodSymbol leftMethod && right is IMethodSymbol rightMethod)
            {
                // If member is a method,
                // compare return type attributes,
                ReportAttributeDifferences(left,
                    leftMetadata,
                    rightMetadata,
                    left.GetDocumentationCommentId() + "->" + leftMethod.ReturnType,
                    leftMethod.GetReturnTypeAttributes(),
                    rightMethod.GetReturnTypeAttributes(),
                    differences);

                // parameter attributes,
                if (leftMethod.Parameters.Length == rightMethod.Parameters.Length)
                {
                    for (int i = 0; i < leftMethod.Parameters.Length; i++)
                    {
                        ReportAttributeDifferences(left,
                            leftMetadata,
                            rightMetadata,
                            left.GetDocumentationCommentId() + $"${i}",
                            leftMethod.Parameters[i].GetAttributes(),
                            rightMethod.Parameters[i].GetAttributes(),
                            differences);
                    }
                }

                // and type parameter attributes.
                if (leftMethod.TypeParameters.Length == rightMethod.TypeParameters.Length)
                {
                    for (int i = 0; i < leftMethod.TypeParameters.Length; i++)
                    {
                        ReportAttributeDifferences(left,
                            leftMetadata,
                            rightMetadata,
                            left.GetDocumentationCommentId() + $"<{i}>",
                            leftMethod.TypeParameters[i].GetAttributes(),
                            rightMethod.TypeParameters[i].GetAttributes(),
                            differences);
                    }
                }
            }

            ReportAttributeDifferences(left,
                leftMetadata,
                rightMetadata,
                left.GetDocumentationCommentId() ?? "",
                left.GetAttributes(),
                right.GetAttributes(),
                differences);
        }

        private class AttributeGroup
        {
            public readonly AttributeData Representative;
            public readonly List<AttributeData> Attributes = new();
            public readonly List<bool> Seen = new();

            public AttributeGroup(AttributeData attributeData)
            {
                Representative = attributeData;
                Add(attributeData);
            }

            public void Add(AttributeData attributeData)
            {
                Attributes.Add(attributeData);
                Seen.Add(false);
            }
        }

        private class AttributeSet : IEnumerable<AttributeGroup>
        {
            // _set holds a set of attribute groups, each represented by an attribute class.
            // We use a List instead of a HashSet because in practice, the number of attributes
            // on a declaration is going to be extremely small (on the order of 1-3).
            private readonly List<AttributeGroup> _set = new();
            private readonly IRuleSettings _settings;

            public AttributeSet(IRuleSettings settings, IList<AttributeData> attributes)
            {
                _settings = settings;
                for (int i = 0; i < attributes.Count; i++)
                {
                    Add(attributes[i]);
                }
            }

            public void Add(AttributeData attributeData)
            {
                if (attributeData.AttributeClass != null && !_settings.SymbolFilter.Include(attributeData.AttributeClass))
                    return;

                foreach (AttributeGroup group in _set)
                {
                    if (_settings.SymbolEqualityComparer.Equals(group.Representative.AttributeClass!, attributeData.AttributeClass!))
                    {
                        group.Add(attributeData);
                        return;
                    }
                }

                _set.Add(new AttributeGroup(attributeData));
            }

            public bool TryGetValue(AttributeData attributeData, [MaybeNullWhen(false)] out AttributeGroup attributeGroup)
            {
                foreach (AttributeGroup group in _set)
                {
                    if (_settings.SymbolEqualityComparer.Equals(group.Representative.AttributeClass!, attributeData.AttributeClass!))
                    {
                        attributeGroup = group;
                        return true;
                    }
                }

                attributeGroup = null!;
                return false;
            }

            public IEnumerator<AttributeGroup> GetEnumerator() => ((IEnumerable<AttributeGroup>)_set).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_set).GetEnumerator();
        }
    }
}
