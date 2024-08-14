// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that generic constraints on type parameters do not change.
    ///   TypeParameter names should not change in strict mode, or when specified
    ///   strict mode: no changes at all
    ///   sealed types and non-virtual methods: no additions, removals permitted
    ///   non-sealed types and virtual methods: no changes
    /// </summary>
    public class CannotChangeGenericConstraints : IRule
    {
        private readonly IRuleSettings _settings;

        public CannotChangeGenericConstraints(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is not INamedTypeSymbol leftType || right is not INamedTypeSymbol rightType)
            {
                return;
            }

            var leftTypeParameters = leftType.TypeParameters;
            var rightTypeParameters = rightType.TypeParameters;

            // can remove constraints on sealed classes since no code should observe broader set of type parameters
            bool permitConstraintRemoval = !_settings.StrictMode && leftType.IsSealed;

            CompareTypeParameters(leftTypeParameters, rightTypeParameters, left, permitConstraintRemoval, leftMetadata, rightMetadata, differences);
        }

        private void RunOnMemberSymbol(
            ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            if (left is not IMethodSymbol leftMethod || right is not IMethodSymbol rightMethod)
            {
                return;
            }

            var leftTypeParameters = leftMethod.TypeParameters;
            var rightTypeParameters = rightMethod.TypeParameters;

            bool permitConstraintRemoval = !_settings.StrictMode && !leftMethod.IsVirtual;

            CompareTypeParameters(leftTypeParameters, rightTypeParameters, left, permitConstraintRemoval, leftMetadata, rightMetadata, differences);
        }

        private void CompareTypeParameters(
            ImmutableArray<ITypeParameterSymbol> leftTypeParameters,
            ImmutableArray<ITypeParameterSymbol> rightTypeParameters,
            ISymbol left,
            bool permitConstraintRemoval,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            Debug.Assert(leftTypeParameters.Length == rightTypeParameters.Length);
            for (int i = 0; i < leftTypeParameters.Length; i++)
            {
                ITypeParameterSymbol leftTypeParam = leftTypeParameters[i];
                ITypeParameterSymbol rightTypeParam = rightTypeParameters[i];

                List<string> addedConstraints = new();
                List<string> removedConstraints = new();

                CompareBoolConstraint(typeParam => typeParam.HasConstructorConstraint, "new()");
                CompareBoolConstraint(typeParam => typeParam.HasNotNullConstraint, "notnull");
                CompareBoolConstraint(typeParam => typeParam.HasReferenceTypeConstraint, "class");
                CompareBoolConstraint(typeParam => typeParam.HasUnmanagedTypeConstraint, "unmanaged");
                // unmanaged implies struct
                CompareBoolConstraint(typeParam => typeParam.HasValueTypeConstraint & !typeParam.HasUnmanagedTypeConstraint, "struct");

                HashSet<ISymbol> rightOnlyConstraints = rightTypeParam.ConstraintTypes.ToHashSet(_settings.SymbolEqualityComparer);
                rightOnlyConstraints.ExceptWith(leftTypeParam.ConstraintTypes);

                // we could allow an addition if removals are allowed, and the addition is a less-derived base type or interface
                // for example: changing a constraint from MemoryStream to Stream on a sealed type, or non-virtual member
                // but we'll leave this to suppressions
                
                addedConstraints.AddRange(rightOnlyConstraints.Select(x => x.GetDocumentationCommentId()!));

                // additions
                foreach(var addedConstraint in addedConstraints) 
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotChangeGenericConstraint,
                        string.Format(Resources.CannotAddGenericConstraint, addedConstraint, leftTypeParam, left),
                        DifferenceType.Added,
                        $"{left.GetDocumentationCommentId()}``{i}:{addedConstraint}"));
                }

                // removals
                // we could allow a removal in the case of reducing to more-derived interfaces if those interfaces were previous constraints
                // for example if IB : IA and a type is constrained by both IA and IB, it's safe to remove IA since it's implied by IB
                // but we'll leave this to suppressions

                if (!permitConstraintRemoval)
                {
                    HashSet<ISymbol> leftOnlyConstraints = leftTypeParam.ConstraintTypes.ToHashSet(_settings.SymbolEqualityComparer);
                    leftOnlyConstraints.ExceptWith(rightTypeParam.ConstraintTypes);
                    removedConstraints.AddRange(leftOnlyConstraints.Select(x => x.GetDocumentationCommentId()!));

                    foreach (var removedConstraint in removedConstraints)
                    {
                        differences.Add(new CompatDifference(
                            leftMetadata,
                            rightMetadata,
                            DiagnosticIds.CannotChangeGenericConstraint,
                            string.Format(Resources.CannotRemoveGenericConstraint, removedConstraint, leftTypeParam, left),
                            DifferenceType.Removed,
                            $"{left.GetDocumentationCommentId()}``{i}:{removedConstraint}"));
                    }
                }

                void CompareBoolConstraint(Func<ITypeParameterSymbol, bool> boolConstraint, string constraintName)
                {
                    bool leftBoolConstraint = boolConstraint(leftTypeParam);
                    bool rightBoolConstraint = boolConstraint(rightTypeParam);

                    // addition
                    if (!leftBoolConstraint && rightBoolConstraint)
                    {
                        addedConstraints.Add(constraintName);
                    }
                    // removal
                    else if (!permitConstraintRemoval && leftBoolConstraint && !rightBoolConstraint)
                    {
                        removedConstraints.Add(constraintName);
                    }
                }
            }
        }
    }
}
