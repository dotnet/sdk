﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class CannotAddMemberToInterface : IRule
    {
        public CannotAddMemberToInterface(IRuleSettings settings, IRuleRegistrationContext context)
        {
            // StrictMode scenario are handled by the MembersMustExist rule.
            if (!settings.StrictMode)
            {
                context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            }
        }

        private void RunOnMemberSymbol(ISymbol? left, ISymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left == null && right != null && right.ContainingType.TypeKind == TypeKind.Interface)
            {
                // Fields in interface can only be static which is not considered a break.
                if (right is IFieldSymbol)
                    return;

                // Event and property accessors are covered by finding the property or event implementation
                // for interface member on the containing type.
                if (right is IMethodSymbol ms && IsEventOrPropertyAccessor(ms))
                    return;

                // If there is a default implementation provided is not a breaking change to add an interface member.
                if (right.ContainingType.FindImplementationForInterfaceMember(right) == null)
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotAddMemberToInterface,
                        string.Format(Resources.CannotAddMemberToInterface, right.ToDisplayString(), rightMetadata, leftMetadata),
                        DifferenceType.Added,
                        right));
                }
            }
        }

        private static bool IsEventOrPropertyAccessor(IMethodSymbol symbol) =>
            symbol.MethodKind == MethodKind.PropertyGet ||
            symbol.MethodKind == MethodKind.PropertySet ||
            symbol.MethodKind == MethodKind.EventAdd ||
            symbol.MethodKind == MethodKind.EventRemove;
    }
}
