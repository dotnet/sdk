// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class CannotRemoveBaseTypeOrInterface : Rule
    {
        public override void Initialize(RuleRunnerContext context)
        {
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            if (left == null || right == null)
                return;

            if (left.TypeKind != TypeKind.Interface && right.TypeKind != TypeKind.Interface)
            {
                // if left and right are not interfaces check base types
                ValidateBaseTypeNotRemoved(left, right, leftName, rightName, differences);

                if (Settings.StrictMode)
                    ValidateBaseTypeNotRemoved(right, left, rightName, leftName, differences);
            }

            ValidateInterfaceNotRemoved(left, right, leftName, rightName, differences);

            if (Settings.StrictMode)
                ValidateInterfaceNotRemoved(right, left, rightName, leftName, differences);
        }
        private void ValidateBaseTypeNotRemoved(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            // Base type order matters in the hierarchy chain so therefore use a list.
            List<ITypeSymbol> rightBaseTypes = right.GetAllBaseTypes().ToList();

            int lastTypeIndex = 0;
            foreach (ITypeSymbol leftBaseType in left.GetAllBaseTypes())
            {
                lastTypeIndex = rightBaseTypes.FindIndex(lastTypeIndex, r => Settings.SymbolComparer.Equals(leftBaseType, r));

                if (lastTypeIndex < 0)
                {
                    differences.Add(new CompatDifference(
                                            DiagnosticIds.CannotRemoveBaseType,
                                            string.Format(Resources.CannotRemoveBaseType, left.ToDisplayString(), leftBaseType.ToDisplayString(), rightName, leftName),
                                            DifferenceType.Changed,
                                            right));
                    return;
                }
            }
        }

        private void ValidateInterfaceNotRemoved(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            HashSet<ITypeSymbol> rightInterfaces = new(right.GetAllBaseInterfaces(), Settings.SymbolComparer);

            foreach (ITypeSymbol leftInterface in left.GetAllBaseInterfaces())
            {
                // Ignore internal interfaces
                if (!leftInterface.IsVisibleOutsideOfAssembly())
                    return;

                if (!rightInterfaces.Contains(leftInterface))
                {
                    differences.Add(new CompatDifference(
                                            DiagnosticIds.CannotRemoveBaseInterface,
                                            string.Format(Resources.CannotRemoveBaseInterface, left.ToDisplayString(), leftInterface.ToDisplayString(), rightName, leftName),
                                            DifferenceType.Changed,
                                            right));
                    return;
                }
            }
        }
    }
}
