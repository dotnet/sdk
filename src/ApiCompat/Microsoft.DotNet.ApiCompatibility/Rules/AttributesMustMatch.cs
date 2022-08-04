// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class AttributesMustMatch : Rule
    {
        private class AttributeGroup : IEqualityComparer<AttributeGroup>
        {
            public AttributeData _repr;
            public List<AttributeData> _attributes;
            public List<bool> _seen;
            private RuleSettings _Settings;

            public AttributeGroup(RuleSettings Settings) { _Settings = Settings; }
            public AttributeGroup(RuleSettings Settings, AttributeData attr)
            {
                _Settings = Settings;
                _repr = attr;
                _attributes = new List<AttributeData>();
                _seen = new List<bool>();
                add(attr);
            }

            public void add(AttributeData attr)
            {
                _attributes.Add(attr);
                _seen.Add(false);
            }

            public bool Equals(AttributeGroup x, AttributeGroup y) => _Settings.SymbolComparer.Equals(x._repr.AttributeClass, y._repr.AttributeClass);
            public int GetHashCode(AttributeGroup obj) => _Settings.SymbolComparer.GetHashCode(obj._repr.AttributeClass);
        }
        private class AttributeSet : IEnumerable<AttributeGroup>
        {
            private HashSet<AttributeGroup> _set;
            private RuleSettings _Settings;

            public AttributeSet(RuleSettings Settings, ImmutableArray<AttributeData> attributes)
            {
                _set = new HashSet<AttributeGroup>(new AttributeGroup(Settings));
                _Settings = Settings;
                for (int i = 0; i < attributes.Length; i++)
                {
                    add(attributes[i]);
                }
            }

            public void add(AttributeData attr)
            {
                var grp = new AttributeGroup(_Settings, attr);
                if (_set.TryGetValue(grp, out AttributeGroup g))
                {
                    g.add(attr);
                }
                else
                {
                    _set.Add(grp);
                }
            }

            public AttributeGroup contains(AttributeData attr)
            {
                var grp = new AttributeGroup(_Settings, attr);
                if (_set.TryGetValue(grp, out AttributeGroup g))
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
        public override void Initialize(RuleRunnerContext context)
        {
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            var leftAttr = new AttributeSet(Settings, left.GetAttributes());
            var rightAttr = new AttributeSet(Settings, right.GetAttributes());
            // commenting this out changes the list of differences.
            reportAttributeDifferences(left, left.GetAttributes(), right.GetAttributes(), differences);
        }

        private CompatDifference removedDifference(ISymbol containing, AttributeData attr)
        {
            // TODO: It should say F() not First.
            string msg = string.Format(Resources.CannotRemoveAttribute, attr, containing);
            return new CompatDifference(DiagnosticIds.CannotRemoveAttribute, msg, DifferenceType.Removed, containing.GetDocumentationCommentId() + ":" + attr.AttributeClass.GetDocumentationCommentId());
        }

        private CompatDifference addedDifference(ISymbol containing, AttributeData attr)
        {
            string msg = string.Format(Resources.CannotAddAttribute, attr, containing);
            return new CompatDifference(DiagnosticIds.CannotAddAttribute, msg, DifferenceType.Added, containing.GetDocumentationCommentId() + ":" + attr.AttributeClass.GetDocumentationCommentId());
        }

        private CompatDifference changedDifference(string rsc, ISymbol containing, AttributeData attr)
        {
            var args = attr.ToString();
            args = args.Substring(args.IndexOf('('));
            string msg = string.Format(rsc, attr.AttributeClass, containing, args);
            return new CompatDifference(DiagnosticIds.CannotChangeAttribute, msg, DifferenceType.Changed, containing.GetDocumentationCommentId() + ":" + attr.AttributeClass.GetDocumentationCommentId());
        }

        private bool attributeEquals(AttributeData left, AttributeData right)
        {
            if (!Settings.SymbolComparer.Equals(left.AttributeClass, right.AttributeClass))
            {
                return false;
            }
            if (!Enumerable.SequenceEqual(left.ConstructorArguments, right.ConstructorArguments))
            {
                return false;
            }
            return Enumerable.SequenceEqual(left.NamedArguments, right.NamedArguments);
        }

        private void reportAttributeDifferences(ISymbol containing,
                                                ImmutableArray<AttributeData> left,
                                                ImmutableArray<AttributeData> right,
                                                IList<CompatDifference> differences)
        {
            var leftAttr = new AttributeSet(Settings, left);
            var rightAttr = new AttributeSet(Settings, right);
            foreach (var lgrp in leftAttr)
            {
                var rgrp = rightAttr.contains(lgrp._repr);
                if (rgrp == null)
                {
                    // exists on left but not on right.
                    // loop over left and issue "removed" diagnostic for each one.
                    for (int i = 0; i < lgrp._attributes.Count; i++)
                    {
                        var lem = lgrp._attributes[i];
                        differences.Add(removedDifference(containing, lem));
                    }
                }
                else
                {
                    for (int i = 0; i < lgrp._attributes.Count; i++)
                    {
                        var lem = lgrp._attributes[i];
                        var seen = false;
                        for (int j = 0; j < rgrp._attributes.Count; j++)
                        {
                            var rem = rgrp._attributes[j];
                            if (attributeEquals(lem, rem))
                            {
                                rgrp._seen[j] = true;
                                seen = true;
                                break;
                            }
                        }
                        if (!seen)
                        {
                            differences.Add(changedDifference(Resources.CannotChangeAttributeFrom, containing, lem));
                            // issue lem exists on left but not right.
                        }
                    }
                    for (int i = 0; i < rgrp._attributes.Count; i++)
                    {
                        if (!rgrp._seen[i])
                        {
                            // issue rem exists on right but not left.
                            var rem = rgrp._attributes[i];
                            differences.Add(changedDifference(Resources.CannotChangeAttributeTo, containing, rem));
                        }
                    }
                }
            }
            foreach (var rgrp in rightAttr)
            {
                var lgrp = leftAttr.contains(rgrp._repr);
                if (rgrp == null)
                {
                    // exists on right but not left.
                    // loop over right and issue "added" diagnostic for each one.
                    for (int i = 0; i < rgrp._attributes.Count; i++)
                    {
                        var rem = rgrp._attributes[i];
                        differences.Add(addedDifference(containing, rem));
                    }
                }
            }
        }

        private void RunOnMemberSymbol(ISymbol left, ISymbol right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, IList<CompatDifference> differences)
        {
            reportAttributeDifferences(left, left.GetAttributes(), right.GetAttributes(), differences);
        }
    }
}
