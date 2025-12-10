// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the 'static' keyword is not added to
    /// or removed from a member or type.
    /// </summary>
    public class CannotAddOrRemoveStaticKeyword : IRule
    {
        private readonly IRuleSettings _settings;

        public CannotAddOrRemoveStaticKeyword(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnMemberSymbol(ISymbol? left, ISymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            // Members must exist
            if (left is null || right is null)
            {
                return;
            }

            // Check if static modifier changed on the member
            if (left.IsStatic && !right.IsStatic)
            {
                // Removing static is always breaking (both binary and source breaking)
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotRemoveStaticFromMember,
                    string.Format(Resources.CannotRemoveStaticFromMember, right),
                    DifferenceType.Removed,
                    right));
            }
            else if (!left.IsStatic && right.IsStatic)
            {
                // Adding static is always breaking (binary breaking)
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotAddStaticToMember,
                    string.Format(Resources.CannotAddStaticToMember, right),
                    DifferenceType.Added,
                    right));
            }
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            // Types must exist
            if (left is null || right is null)
            {
                return;
            }

            // Check if static modifier was added to the type
            // Adding static to a type is breaking since static types cannot be used as 
            // return types, generic parameters, or arguments
            // Removing static from a type is compatible (members will be checked separately)
            if (!left.IsStatic && right.IsStatic)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotAddStaticToType,
                    string.Format(Resources.CannotAddStaticToType, right),
                    DifferenceType.Added,
                    right));
            }
        }
    }
}
