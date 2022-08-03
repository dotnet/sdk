// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
            reportTwo(left, left.GetAttributes(), right.GetAttributes(), differences);
            // reportAttributeDifferences(left, left.GetAttributes(), right.GetAttributes(), differences);
        }

        private CompatDifference removedDifference(ISymbol containing, AttributeData attr)
        {
            string msg = string.Format(Resources.CannotRemoveAttribute, attr, containing);
            return new CompatDifference(DiagnosticIds.CannotRemoveAttribute, msg, DifferenceType.Removed, attr.AttributeClass);
        }

        private CompatDifference addedDifference(ISymbol containing, AttributeData attr)
        {
            string msg = string.Format(Resources.CannotAddAttribute, attr, containing);
            return new CompatDifference(DiagnosticIds.CannotAddAttribute, msg, DifferenceType.Added, attr.AttributeClass);
        }

        private CompatDifference changedDifference(string arg, AttributeData attr)
        {
            string msg = string.Format(Resources.CannotChangeAttribute, arg, attr);
            return new CompatDifference(DiagnosticIds.CannotChangeAttribute, msg, DifferenceType.Changed, attr.AttributeClass);
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
                                                ImmutableArray<AttributeData> leftAttr,
                                                ImmutableArray<AttributeData> rightAttr,
                                                IList<CompatDifference> differences)
        {
            // TODO: For duplicate sole names.
            // TODO: message "expected args _"
            var len = Math.Min(leftAttr.Length, rightAttr.Length);
            for (int i = 0; i < len; i++)
            {
                if (!Settings.SymbolComparer.Equals(leftAttr[i].AttributeClass, rightAttr[i].AttributeClass))
                {
                    differences.Add(removedDifference(containing, leftAttr[i]));
                    differences.Add(addedDifference(containing, rightAttr[i]));
                    continue;
                }
                var leftArgs = leftAttr[i].ConstructorArguments;
                var rightArgs = rightAttr[i].ConstructorArguments;
                var argLen = Math.Min(leftArgs.Length, rightArgs.Length);
                for (int j = 0; j < argLen; j++)
                {
                    if (!leftArgs[j].Equals(rightArgs[j]))
                    {
                        differences.Add(changedDifference(j.ToString(), leftAttr[i]));
                    }
                }
                if (leftArgs.Length > rightArgs.Length)
                {
                    for (int j = argLen; j < leftArgs.Length; j++)
                    {
                        differences.Add(changedDifference(j.ToString(), leftAttr[i]));
                    }
                }
                else if (leftArgs.Length < rightArgs.Length)
                {
                    for (int j = argLen; j < rightArgs.Length; j++)
                    {
                        differences.Add(changedDifference(j.ToString(), rightAttr[i]));
                    }
                }
                var leftNamed = leftAttr[i].NamedArguments;
                var rightNamed = rightAttr[i].NamedArguments;
                var namedLen = Math.Min(leftNamed.Length, rightNamed.Length);
                for (int j = 0; j < namedLen; j++)
                {
                    if (!leftNamed[j].Equals(rightNamed[j]))
                    {
                        differences.Add(changedDifference(leftNamed[j].Key, leftAttr[i]));
                    }
                }
                if (leftNamed.Length > rightNamed.Length)
                {
                    for (int j = namedLen; j < leftNamed.Length; j++)
                    {
                        // Named argument removed
                        differences.Add(changedDifference(leftNamed[j].Key, leftAttr[i]));
                    }
                }
                else if (leftNamed.Length < rightNamed.Length)
                {
                    for (int j = namedLen; j < rightNamed.Length; j++)
                    {
                        // Named argument added
                        differences.Add(changedDifference(rightNamed[j].Key, rightAttr[i]));
                    }
                }
            }
            if (leftAttr.Length > rightAttr.Length)
            {
                for (int j = len; j < leftAttr.Length; j++)
                {
                    differences.Add(removedDifference(containing, leftAttr[j]));
                }
            }
            else if (leftAttr.Length < rightAttr.Length)
            {
                for (int j = len; j < rightAttr.Length; j++)
                {
                    differences.Add(addedDifference(containing, rightAttr[j]));
                }
            }
        }

        private void reportTwo(ISymbol containing,
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
                            if (!rgrp._seen[j])
                            {
                                var rem = rgrp._attributes[j];
                                if (attributeEquals(lem, rem))
                                {
                                    rgrp._seen[j] = true;
                                    seen = true;
                                    break;
                                }
                            }
                        }
                        if (!seen)
                        {
                            // issue lem exists on left but not right.
                            differences.Add(new CompatDifference(DiagnosticIds.CannotChangeAttribute, "LEM exists on left but not right", DifferenceType.Changed, lem.AttributeClass));
                        }
                    }
                    for (int i = 0; i < rgrp._attributes.Count; i++)
                    {
                        if (!rgrp._seen[i])
                        {
                            // issue rem exists on right but not left.
                            var rem = rgrp._attributes[i];
                            differences.Add(new CompatDifference(DiagnosticIds.CannotChangeAttribute, "REM exists on right but not left", DifferenceType.Changed, rem.AttributeClass));
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
            reportTwo(left, left.GetAttributes(), right.GetAttributes(), differences);
            // reportAttributeDifferences(left, left.GetAttributes(), right.GetAttributes(), differences);
        }
    }
}
