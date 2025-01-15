// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This rule validates that base types and interfaces aren't removed from the right.
    /// In strict mode, it also validates that the right doesn't add base types and interfaces.
    /// </summary>
    public class CannotRemoveBaseTypeOrInterface : IRule
    {
        private readonly IRuleSettings _settings;

        public CannotRemoveBaseTypeOrInterface(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left == null || right == null)
                return;

            if (left.TypeKind != TypeKind.Interface && right.TypeKind != TypeKind.Interface)
            {
                // if left and right are not interfaces check base types
                ValidateBaseTypeNotRemoved(left, right, leftMetadata.DisplayString, rightMetadata.DisplayString, leftMetadata, rightMetadata, differences);

                if (_settings.StrictMode)
                    ValidateBaseTypeNotRemoved(right, left, rightMetadata.DisplayString, leftMetadata.DisplayString, leftMetadata, rightMetadata, differences);
            }

            ValidateInterfaceNotRemoved(left, right, leftMetadata.DisplayString, rightMetadata.DisplayString, leftMetadata, rightMetadata, differences);

            if (_settings.StrictMode)
                ValidateInterfaceNotRemoved(right, left, rightMetadata.DisplayString, leftMetadata.DisplayString, leftMetadata, rightMetadata, differences);
        }

        private void ValidateBaseTypeNotRemoved(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            ITypeSymbol? leftBaseType = left.BaseType;
            ITypeSymbol? rightBaseType = right.BaseType;

            if (leftBaseType == null)
                return;

            while (rightBaseType != null)
            {
                // If we found the immediate left base type on right we can assume
                // that any removal of a base type up on the hierarchy will be handled
                // when validating the type which it's base type was actually removed.
                if (_settings.SymbolEqualityComparer.Equals(leftBaseType, rightBaseType))
                    return;

                rightBaseType = rightBaseType.BaseType;
            }

            differences.Add(new CompatDifference(
                leftMetadata,
                rightMetadata,
                DiagnosticIds.CannotRemoveBaseType,
                string.Format(Resources.CannotRemoveBaseType, left.ToDisplayStringWithAccessibility(_settings.IncludeInternalSymbols), leftBaseType.ToDisplayStringWithAccessibility(_settings.IncludeInternalSymbols), rightName, leftName),
                DifferenceType.Changed,
                right));
        }

        private void ValidateInterfaceNotRemoved(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            HashSet<ITypeSymbol> rightInterfaces = new(right.GetAllBaseInterfaces(), _settings.SymbolEqualityComparer);

            foreach (ITypeSymbol leftInterface in left.GetAllBaseInterfaces())
            {
                // Ignore non visible interfaces based on the run Settings
                // If TypeKind == Error it means the Roslyn couldn't resolve it,
                // so we are running with a missing assembly reference to where that type ef is defined.
                // However we still want to consider it as Roslyn does resolve it's name correctly.
                if (!leftInterface.IsVisibleOutsideOfAssembly(_settings.IncludeInternalSymbols) && leftInterface.TypeKind != TypeKind.Error)
                    return;

                if (!rightInterfaces.Contains(leftInterface))
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotRemoveBaseInterface,
                        string.Format(Resources.CannotRemoveBaseInterface, left.ToDisplayStringWithAccessibility(_settings.IncludeInternalSymbols), leftInterface.ToDisplayStringWithAccessibility(_settings.IncludeInternalSymbols), rightName, leftName),
                        DifferenceType.Changed,
                        right));
                    return;
                }
            }
        }
    }
}
