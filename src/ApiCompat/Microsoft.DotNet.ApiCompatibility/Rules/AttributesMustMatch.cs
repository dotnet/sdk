// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the attributes between public members do not change.
    /// </summary>
    public class AttributesMustMatch : IRule
    {
        private readonly RuleSettings _settings;

        public AttributesMustMatch(RuleSettings settings, IRuleRegistrationContext context, IEnumerable<string>? excludeAttributesFiles)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left is null || right is null)
            {
                return;
            }

            if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed)
            {
                if (leftNamed.TypeParameters.Length == rightNamed.TypeParameters.Length)
                {
                    for (int i = 0; i < leftNamed.TypeParameters.Length; i++)
                    {
                        ReportAttributeDifferences(left, left.GetDocumentationCommentId() + $"<{i}>", leftNamed.TypeParameters[i].GetAttributes(), rightNamed.TypeParameters[i].GetAttributes(), differences);
                    }
                }
            }

            ReportAttributeDifferences(left, left.GetDocumentationCommentId() ?? "", left.GetAttributes(), right.GetAttributes(), differences);
        }

        private static CompatDifference RemovedDifference(ISymbol containing, string itemRef, AttributeData? attr) => CompatDifference.CreateWithDefaultMetadata(
            DiagnosticIds.CannotRemoveAttribute,
            string.Format(Resources.CannotRemoveAttribute, attr, containing),
            DifferenceType.Removed,
            itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");

        private static CompatDifference AddedDifference(ISymbol containing, string itemRef, AttributeData? attr) => CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute,
            string.Format(Resources.CannotAddAttribute, attr, containing),
            DifferenceType.Added,
            itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");

        private static CompatDifference ChangedDifference(ISymbol containing, string itemRef, AttributeData? attr) => CompatDifference.CreateWithDefaultMetadata(
            DiagnosticIds.CannotChangeAttribute,
            string.Format(Resources.CannotChangeAttribute, attr?.AttributeClass, containing),
            DifferenceType.Changed,
            itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");

        private bool AttributeEquals(AttributeData? left, AttributeData? right)
        {
            if (left != null && right != null)
            {
                if (!_settings.SymbolComparer.Equals(left.AttributeClass!, right.AttributeClass!))
                {
                    return false;
                }

                if (!Enumerable.SequenceEqual(left.ConstructorArguments, right.ConstructorArguments))
                {
                    return false;
                }

                return Enumerable.SequenceEqual(left.NamedArguments, right.NamedArguments);
            }
            return left == right;
        }

        private void ReportAttributeDifferences(ISymbol containing,
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

            var leftAttr = new AttributeSet(_settings, left);
            var rightAttr = new AttributeSet(_settings, right);

            foreach (AttributeGroup lgrp in leftAttr)
            {
                if (rightAttr.TryGetValue(lgrp.Representative, out AttributeGroup? rgrp))
                {
                    for (int i = 0; i < lgrp.Attributes!.Count; i++) // TODO: this exclamation point shouldn't be necessary.
                    {
                        AttributeData? lem = lgrp.Attributes[i];
                        bool seen = false;
                        for (int j = 0; j < rgrp.Attributes.Count; j++)
                        {
                            AttributeData? rem = rgrp.Attributes[j];
                            if (AttributeEquals(lem, rem))
                            {
                                rgrp.Seen[j] = true;
                                seen = true;
                                break;
                            }
                        }

                        if (!seen)
                        {
                            // issue lem exists on left but not right.
                            differences.Add(AttributesMustMatch.ChangedDifference(containing, itemRef, lem));
                        }
                    }

                    for (int i = 0; i < rgrp.Attributes.Count; i++)
                    {
                        if (!rgrp.Seen[i])
                        {
                            // issue rem exists on right but not left.
                            AttributeData? rem = rgrp.Attributes[i];
                            differences.Add(AttributesMustMatch.ChangedDifference(containing, itemRef, rem));
                        }
                    }
                }
                else
                {
                    // exists on left but not on right.
                    // loop over left and issue "removed" diagnostic for each one.
                    for (int i = 0; i < lgrp.Attributes?.Count; i++)
                    {
                        AttributeData? lem = lgrp.Attributes[i];
                        differences.Add(AttributesMustMatch.RemovedDifference(containing, itemRef, lem));
                    }
                }
            }

            foreach (AttributeGroup rgrp in rightAttr)
            {
                if (leftAttr.TryGetValue(rgrp.Representative, out _))
                {
                    continue;
                }

                // exists on right but not left.
                // loop over right and issue "added" diagnostic for each one.
                for (int i = 0; i < rgrp.Attributes?.Count; i++)
                {
                    AttributeData? rem = rgrp.Attributes[i];
                    differences.Add(AttributesMustMatch.AddedDifference(containing, itemRef, rem));
                }
            }
        }

        private void RunOnMemberSymbol(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left is null || right is null)
            {
                return;
            }

            if (left is IMethodSymbol leftMethod && right is IMethodSymbol rightMethod)
            {
                ReportAttributeDifferences(left, left.GetDocumentationCommentId() + "->" + leftMethod.ReturnType, leftMethod.GetReturnTypeAttributes(), rightMethod.GetReturnTypeAttributes(), differences);

                if (leftMethod.Parameters.Length == rightMethod.Parameters.Length)
                {
                    for (int i = 0; i < leftMethod.Parameters.Length; i++)
                    {
                        ReportAttributeDifferences(left, left.GetDocumentationCommentId() + $"${i}", leftMethod.Parameters[i].GetAttributes(), rightMethod.Parameters[i].GetAttributes(), differences);
                    }
                }

                if (leftMethod.TypeParameters.Length == rightMethod.TypeParameters.Length)
                {
                    for (int i = 0; i < leftMethod.TypeParameters.Length; i++)
                    {
                        ReportAttributeDifferences(left, left.GetDocumentationCommentId() + $"<{i}>", leftMethod.TypeParameters[i].GetAttributes(), rightMethod.TypeParameters[i].GetAttributes(), differences);
                    }
                }
            }

            ReportAttributeDifferences(left, left.GetDocumentationCommentId() ?? "", left.GetAttributes(), right.GetAttributes(), differences);
        }

        private class AttributeGroup
        {
            public readonly AttributeData Representative;
            public readonly List<AttributeData> Attributes = new();
            public readonly List<bool> Seen = new();

            public AttributeGroup(AttributeData attr)
            {
                Representative = attr;
                Seen = new List<bool>();
                Add(attr);
            }

            public void Add(AttributeData attr)
            {
                Attributes.Add(attr);
                Seen.Add(false);
            }
        }
        private class AttributeSet : IEnumerable<AttributeGroup>
        {
            private readonly List<AttributeGroup> _set;
            private readonly RuleSettings _settings;

            public AttributeSet(RuleSettings Settings, IList<AttributeData> attributes)
            {
                _set = new List<AttributeGroup>();
                _settings = Settings;
                for (int i = 0; i < attributes.Count; i++)
                {
                    Add(attributes[i]);
                }
            }

            public void Add(AttributeData attr)
            {
                foreach (AttributeGroup group in _set)
                {
                    if (_settings.SymbolComparer.Equals(group.Representative.AttributeClass!, attr.AttributeClass!))
                    {
                        group.Add(attr);
                        return;
                    }
                }

                _set.Add(new AttributeGroup(attr));
            }

            public bool TryGetValue(AttributeData attr, [MaybeNullWhen(false)] out AttributeGroup attributeGroup)
            {
                foreach (AttributeGroup group in _set)
                {
                    if (_settings.SymbolComparer.Equals(group.Representative.AttributeClass!, attr.AttributeClass!))
                    {
                        attributeGroup = group;
                        return true;
                    }
                }

                attributeGroup = null;
                return false;
            }

            public IEnumerator<AttributeGroup> GetEnumerator() => ((IEnumerable<AttributeGroup>)_set).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_set).GetEnumerator();
        }
    }
}
