// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the constant values for an enum's fields don't change.
    /// </summary>
    public class EnumValuesMustMatch : Rule
    {
        public override void Initialize(RuleRunnerContext context) => context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);

        private bool isEnum(ITypeSymbol sym) => sym is not null && sym.TypeKind == TypeKind.Enum;

        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            // Ensure that this rule only runs on enums.
            if (isEnum(left) && isEnum(right))
            {
                // Get the underlying value types.
                INamedTypeSymbol leftType = ((INamedTypeSymbol)left).EnumUnderlyingType;
                INamedTypeSymbol rightType = ((INamedTypeSymbol)right).EnumUnderlyingType;

                // Build a map of the enum's fields, keyed by the field names.
                Dictionary<string, IFieldSymbol> leftMembers = left.GetMembers()
                    .Where(a => a.Kind == SymbolKind.Field)
                    .Select(a => (IFieldSymbol)a)
                    .ToDictionary(a => a.Name);
                Dictionary<string, IFieldSymbol> rightMembers = right.GetMembers()
                    .Where(a => a.Kind == SymbolKind.Field)
                    .Select(a => (IFieldSymbol)a)
                    .ToDictionary(a => a.Name);

                // For each field that is present in the left and right, check that their constant values match.
                // Otherwise, emit a diagnostic.
                foreach (KeyValuePair<string, IFieldSymbol> lEntry in leftMembers)
                {
                    if (rightMembers.TryGetValue(lEntry.Key, out IFieldSymbol rField) && !lEntry.Value.ConstantValue.Equals(rField.ConstantValue))
                    {
                        string msg = string.Format(Resources.EnumValuesMustMatch, left.Name, lEntry.Key, leftType, lEntry.Value.ConstantValue, rightType, rField.ConstantValue);
                        differences.Add(new CompatDifference(DiagnosticIds.EnumValuesMustMatch, msg, DifferenceType.Changed, rField));
                    }
                }
            }
        }
    }
}
