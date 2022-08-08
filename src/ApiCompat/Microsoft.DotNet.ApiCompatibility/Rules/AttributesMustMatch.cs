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

        private class AttributeGroup : IEqualityComparer<AttributeGroup>
        {
            public AttributeData? _repr;
            public List<AttributeData?>? _attributes;
            public List<bool>? _seen;
            private RuleSettings _Settings;

            public AttributeGroup(RuleSettings Settings) { _Settings = Settings; }
            public AttributeGroup(RuleSettings Settings, AttributeData? attr)
            {
                _Settings = Settings;
                _repr = attr;
                _attributes = new List<AttributeData?>();
                _seen = new List<bool>();
                add(attr);
            }

            public void add(AttributeData? attr)
            {
                _attributes?.Add(attr);
                _seen?.Add(false);
            }

            public bool Equals(AttributeGroup? x, AttributeGroup? y) => _Settings.SymbolComparer.Equals(x?._repr?.AttributeClass!, y?._repr?.AttributeClass!);
            public int GetHashCode(AttributeGroup? obj) => _Settings.SymbolComparer.GetHashCode(obj?._repr?.AttributeClass!);
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
                    add(attributes[i]);
                }
            }

            public void add(AttributeData attr)
            {
                var grp = new AttributeGroup(_settings, attr);
                if (_set.TryGetValue(grp, out AttributeGroup? g))
                {
                    g.add(attr);
                }
                else
                {
                    _set.Add(grp);
                }
            }

            public AttributeGroup? contains(AttributeData? attr)
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

        public AttributesMustMatch(RuleSettings settings, RuleRunnerContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, string leftName, string rightName, IList<CompatDifference> differences)
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
                        reportAttributeDifferences(left, left.GetDocumentationCommentId() + $"<{i}>", leftNamed.TypeParameters[i].GetAttributes(), rightNamed.TypeParameters[i].GetAttributes(), differences);
                    }
                }
            }
            reportAttributeDifferences(left, left.GetDocumentationCommentId() ?? "", left.GetAttributes(), right.GetAttributes(), differences);
        }

        private static CompatDifference removedDifference(ISymbol containing, string itemRef, AttributeData? attr)
        {
            string msg = string.Format(Resources.CannotRemoveAttribute, attr, containing);
            return new CompatDifference(DiagnosticIds.CannotRemoveAttribute, msg, DifferenceType.Removed, itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");
        }

        private static CompatDifference addedDifference(ISymbol containing, string itemRef, AttributeData? attr)
        {
            string msg = string.Format(Resources.CannotAddAttribute, attr, containing);
            return new CompatDifference(DiagnosticIds.CannotAddAttribute, msg, DifferenceType.Added, itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");
        }

        private static CompatDifference changedDifference(ISymbol containing, string itemRef, AttributeData? attr)
        {
            string msg = string.Format(Resources.CannotChangeAttribute, attr?.AttributeClass, containing);
            return new CompatDifference(DiagnosticIds.CannotChangeAttribute, msg, DifferenceType.Changed, itemRef + ":[" + attr?.AttributeClass?.GetDocumentationCommentId() + "]");
        }

        private bool attributeEquals(AttributeData? left, AttributeData? right)
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

        private void reportAttributeDifferences(ISymbol containing,
                                                string itemRef,
                                                IList<AttributeData> left,
                                                IList<AttributeData> right,
                                                IList<CompatDifference> differences)
        {
            var leftAttr = new AttributeSet(_settings, left);
            var rightAttr = new AttributeSet(_settings, right);
            foreach (AttributeGroup lgrp in leftAttr)
            {
                AttributeGroup? rgrp = rightAttr.contains(lgrp._repr);
                if (rgrp == null)
                {
                    // exists on left but not on right.
                    // loop over left and issue "removed" diagnostic for each one.
                    for (int i = 0; i < lgrp._attributes?.Count; i++)
                    {
                        AttributeData? lem = lgrp._attributes[i];
                        differences.Add(AttributesMustMatch.removedDifference(containing, itemRef, lem));
                    }
                }
                else
                {
                    if (lgrp._attributes == null || lgrp._seen == null || lgrp._repr == null)
                    {
                        continue;
                    }
                    if (rgrp._attributes == null || rgrp._seen == null || rgrp._repr == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < lgrp._attributes.Count; i++)
                    {
                        AttributeData? lem = lgrp._attributes[i];
                        bool seen = false;
                        for (int j = 0; j < rgrp._attributes.Count; j++)
                        {
                            AttributeData? rem = rgrp._attributes[j];
                            if (attributeEquals(lem, rem))
                            {
                                rgrp._seen[j] = true;
                                seen = true;
                                break;
                            }
                        }
                        if (!seen)
                        {
                            differences.Add(AttributesMustMatch.changedDifference(containing, itemRef, lem));
                            // issue lem exists on left but not right.
                        }
                    }
                    for (int i = 0; i < rgrp._attributes.Count; i++)
                    {
                        if (!rgrp._seen[i])
                        {
                            // issue rem exists on right but not left.
                            AttributeData? rem = rgrp._attributes[i];
                            differences.Add(AttributesMustMatch.changedDifference(containing, itemRef, rem));
                        }
                    }
                }
            }
            foreach (var rgrp in rightAttr)
            {
                AttributeGroup? lgrp = leftAttr.contains(rgrp._repr);
                if (lgrp == null)
                {
                    // exists on right but not left.
                    // loop over right and issue "added" diagnostic for each one.
                    for (int i = 0; i < rgrp._attributes?.Count; i++)
                    {
                        AttributeData? rem = rgrp._attributes[i];
                        differences.Add(AttributesMustMatch.addedDifference(containing, itemRef, rem));
                    }
                }
            }
        }

        private void RunOnMemberSymbol(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, IList<CompatDifference> differences)
        {
            if (left is null || right is null)
            {
                return;
            }
            if (left is IMethodSymbol leftMethod && right is IMethodSymbol rightMethod)
            {
                reportAttributeDifferences(left, left.GetDocumentationCommentId() + "->" + leftMethod.ReturnType, leftMethod.GetReturnTypeAttributes(), rightMethod.GetReturnTypeAttributes(), differences);
                if (leftMethod.Parameters.Length == rightMethod.Parameters.Length)
                {
                    for (int i = 0; i < leftMethod.Parameters.Length; i++)
                    {
                        reportAttributeDifferences(left, left.GetDocumentationCommentId() + $"${i}", leftMethod.Parameters[i].GetAttributes(), rightMethod.Parameters[i].GetAttributes(), differences);
                    }
                }
                if (leftMethod.TypeParameters.Length == rightMethod.TypeParameters.Length)
                {
                    for (int i = 0; i < leftMethod.TypeParameters.Length; i++)
                    {
                        reportAttributeDifferences(left, left.GetDocumentationCommentId() + $"<{i}>", leftMethod.TypeParameters[i].GetAttributes(), rightMethod.TypeParameters[i].GetAttributes(), differences);
                    }
                }
            }
            reportAttributeDifferences(left, left.GetDocumentationCommentId() ?? "", left.GetAttributes(), right.GetAttributes(), differences);
        }
    }
}
