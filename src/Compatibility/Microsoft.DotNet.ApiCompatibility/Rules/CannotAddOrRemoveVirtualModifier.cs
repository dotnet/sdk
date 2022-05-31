// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

#nullable enable

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class CannotAddOrRemoveVirtualModifier : Rule
    {
        public override void Initialize(RuleRunnerContext context) => context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);

        private void RunOnMemberSymbol(ISymbol left, ISymbol right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, IList<CompatDifference> differences)
        {
            // Members must exist
            if (left is null || right is null)
            {
                return;
            }
            // If the left member is virtual, it must not be sealed or inside a sealed type.
            if (left.IsVirtual)
            {
                // If the right member is not virtual, emit a diagnostic
                // that the virtual modifier cannot be removed.
                if (!right.IsVirtual)
                {
                    differences.Add(new CompatDifference(
                    DiagnosticIds.CannotRemoveVirtualModifier, string.Format(
                        Resources.CannotRemoveVirtualModifier, left), DifferenceType.Removed, right));
                }
            }
            // Otherwise if the left member is not virtual, ensure that
            // we're either in strict mode, or the left member is not
            // abstract (since it is legal to change abstract to virtual).
            else if (Settings.StrictMode || !left.IsAbstract)
            {
                // If the right member is virtual, emit a diagnostic
                // that the virtual modifier cannot be added.
                if (right.IsVirtual)
                {
                    differences.Add(new CompatDifference(
                    DiagnosticIds.CannotAddVirtualModifier, string.Format(
                        Resources.CannotAddVirtualModifier, right), DifferenceType.Added, right));
                }
            }
        }
    }
}
