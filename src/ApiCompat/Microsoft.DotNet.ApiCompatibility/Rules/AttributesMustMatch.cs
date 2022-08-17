// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime;
using System.Text;
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

        private static CompatDifference RemovedDifference(ISymbol containing, string itemRef, AttributeData? attr)
        {
            string msg = string.Format(Resources.CannotRemoveAttribute, attr, containing);
            return CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotRemoveAttribute, msg, DifferenceType.Removed, itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");
        }

        private static CompatDifference AddedDifference(ISymbol containing, string itemRef, AttributeData? attr)
        {
            string msg = string.Format(Resources.CannotAddAttribute, attr, containing);
            return CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddAttribute, msg, DifferenceType.Added, itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");
        }

        private static CompatDifference ChangedDifference(ISymbol containing, string itemRef, AttributeData? attr)
        {
            string msg = string.Format(Resources.CannotChangeAttribute, attr?.AttributeClass, containing);
            return CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotChangeAttribute, msg, DifferenceType.Changed, itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");
        }

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
            var leftAttr = new AttributeSet(_settings, left);
            var rightAttr = new AttributeSet(_settings, right);
            foreach (AttributeGroup lgrp in leftAttr)
            {
                AttributeGroup? rgrp = rightAttr.Contains(lgrp.Representative);
                if (rgrp == null)
                {
                    // exists on left but not on right.
                    // loop over left and issue "removed" diagnostic for each one.
                    for (int i = 0; i < lgrp.Attributes?.Count; i++)
                    {
                        AttributeData? lem = lgrp.Attributes[i];
                        differences.Add(AttributesMustMatch.RemovedDifference(containing, itemRef, lem));
                    }
                }
                else
                {
                    if (lgrp.Attributes == null || lgrp.Seen == null || lgrp.Representative == null)
                    {
                        continue;
                    }
                    if (rgrp.Attributes == null || rgrp.Seen == null || rgrp.Representative == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < lgrp.Attributes.Count; i++)
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
                            differences.Add(AttributesMustMatch.ChangedDifference(containing, itemRef, lem));
                            // issue lem exists on left but not right.
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
            }
            foreach (var rgrp in rightAttr)
            {
                AttributeGroup? lgrp = leftAttr.Contains(rgrp.Representative);
                if (lgrp == null)
                {
                    // exists on right but not left.
                    // loop over right and issue "added" diagnostic for each one.
                    for (int i = 0; i < rgrp.Attributes?.Count; i++)
                    {
                        AttributeData? rem = rgrp.Attributes[i];
                        differences.Add(AttributesMustMatch.AddedDifference(containing, itemRef, rem));
                    }
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

        private class AttributeGroup : IEqualityComparer<AttributeGroup>
        {
            public readonly AttributeData? Representative;
            public readonly List<AttributeData?>? Attributes;
            public readonly List<bool>? Seen;
            private readonly RuleSettings _settings;

            public AttributeGroup(RuleSettings Settings) { _settings = Settings; }
            public AttributeGroup(RuleSettings Settings, AttributeData? attr)
            {
                _settings = Settings;
                Representative = attr;
                Attributes = new List<AttributeData?>();
                Seen = new List<bool>();
                Add(attr);
            }

            public void Add(AttributeData? attr)
            {
                Attributes?.Add(attr);
                Seen?.Add(false);
            }

            public bool Equals(AttributeGroup? x, AttributeGroup? y) => _settings.SymbolComparer.Equals(x?.Representative?.AttributeClass!, y?.Representative?.AttributeClass!);
            public int GetHashCode(AttributeGroup? obj) => _settings.SymbolComparer.GetHashCode(obj?.Representative?.AttributeClass!);
        }
        private class AttributeSet : IEnumerable<AttributeGroup>
        {
            private HashSet<AttributeGroup> _set;
            private readonly RuleSettings _settings;

            public AttributeSet(RuleSettings Settings, IList<AttributeData> attributes)
            {
                _set = new HashSet<AttributeGroup>(new AttributeGroup(Settings));
                _settings = Settings;
                for (int i = 0; i < attributes.Count; i++)
                {
                    Add(attributes[i]);
                }
            }

            public void Add(AttributeData attr)
            {
                var grp = new AttributeGroup(_settings, attr);
                if (_set.TryGetValue(grp, out AttributeGroup? g))
                {
                    g.Add(attr);
                }
                else
                {
                    _set.Add(grp);
                }
            }

            public AttributeGroup? Contains(AttributeData? attr)
            {
                var grp = new AttributeGroup(_settings, attr);
                if (_set.TryGetValue(grp, out AttributeGroup? g))
                {
                    return g;
                }
                else
                {
                    return null;
                }
            }

            public IEnumerator<AttributeGroup> GetEnumerator() => ((IEnumerable<AttributeGroup>)_set).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_set).GetEnumerator();
        }
    }
}
