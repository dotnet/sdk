// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class AttributesMustMatch : Rule
    {
        public override void Initialize(RuleRunnerContext context)
        {
            // TODO: Right now, only RunOnTypeSymbol is executed because RuleRunner does not allow a single rule to map over types and members.
            // Either change RuleRunner, or add another Rule for the Attributes changing on types.
            // context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            reportAttributeDifferences(left, left.GetAttributes(), right.GetAttributes(), differences);
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

        private void reportAttributeDifferences(ISymbol containing, ImmutableArray<AttributeData> leftAttr, ImmutableArray<AttributeData> rightAttr, IList<CompatDifference> differences)
        {
            // TODO: Right now, we take the naive approach of asserting that attributes are defined in order.
            // Is attribute behavior even order-sensitive?
            // However, this may lead to confusing diagnostics, where an attribute exists but is in a different
            // position in the list. Maybe change this behavior to treat attributes as sets?
            // However, even then, we have to take into account differences in arguments passed.
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
        private void RunOnMemberSymbol(ISymbol left, ISymbol right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, IList<CompatDifference> differences)
        {
            reportAttributeDifferences(left, left.GetAttributes(), right.GetAttributes(), differences);
        }
    }
}
