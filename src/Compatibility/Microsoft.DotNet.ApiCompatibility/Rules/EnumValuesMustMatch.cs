// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class EnumValuesMustMatch : Rule
    {
        public override void Initialize(RuleRunnerContext context)
        {
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private static bool isEnum(ITypeSymbol type)
        {
            return type != null && type.IsType && type.TypeKind == TypeKind.Enum;
        }
        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            if (!isEnum(left) || !isEnum(right))
            {
                return;
            }
            var leftMembers = left.GetMembers().Where(a => a.Kind == SymbolKind.Field).Select(a => ((IFieldSymbol)a)).ToDictionary(a => a.Name);
            var rightMembers = right.GetMembers().Where(a => a.Kind == SymbolKind.Field).Select(a => ((IFieldSymbol)a)).ToDictionary(a => a.Name);
            foreach (var entry in leftMembers)
            {
                IFieldSymbol rField;
                if (rightMembers.TryGetValue(entry.Key, out rField) && !entry.Value.ConstantValue.Equals(rField.ConstantValue))
                {
                    var msg = string.Format(Resources.EnumValuesMustMatch, left.Name, entry.Key, entry.Value.ConstantValue, rField.ConstantValue);
                    differences.Add(new CompatDifference(DiagnosticIds.EnumValuesMustMatch, msg, DifferenceType.Changed, rField));
                }
            }
        }
    }
}
