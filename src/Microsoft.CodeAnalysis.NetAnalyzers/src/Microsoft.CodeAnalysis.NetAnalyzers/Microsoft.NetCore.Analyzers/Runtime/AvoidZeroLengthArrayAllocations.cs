// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>Base type for an analyzer that looks for empty array allocations and recommends their replacement.</summary>
    public abstract class AvoidZeroLengthArrayAllocationsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1825";

        /// <summary>The name of the array type.</summary>
        internal const string ArrayTypeName = "System.Array"; // using instead of GetSpecialType to make more testable

        /// <summary>The name of the Empty method on System.Array.</summary>
        internal const string ArrayEmptyMethodName = "Empty";

        private static readonly SymbolDisplayFormat ReportFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidZeroLengthArrayAllocationsTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidZeroLengthArrayAllocationsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        /// <summary>The diagnostic descriptor used when Array.Empty should be used instead of a new array allocation.</summary>
        internal static readonly DiagnosticDescriptor UseArrayEmptyDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        /// <summary>Gets the set of supported diagnostic descriptors from this analyzer.</summary>
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(UseArrayEmptyDescriptor);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // When compilation begins, check whether Array.Empty<T> is available.
            // Only if it is, register the syntax node action provided by the derived implementations.
            context.RegisterCompilationStartAction(ctx =>
            {
                INamedTypeSymbol? typeSymbol = ctx.Compilation.GetOrCreateTypeByMetadataName(ArrayTypeName);
                if (typeSymbol != null && typeSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    if (typeSymbol.GetMembers(ArrayEmptyMethodName).FirstOrDefault() is IMethodSymbol methodSymbol && methodSymbol.DeclaredAccessibility == Accessibility.Public &&
    methodSymbol.IsStatic && methodSymbol.Arity == 1 && methodSymbol.Parameters.IsEmpty)
                    {
                        ctx.RegisterOperationAction(AnalyzeOperation, OperationKind.ArrayCreation);
                    }
                }
            });
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            AnalyzeOperation(context, IsAttributeSyntax);
        }

        private static void AnalyzeOperation(OperationAnalysisContext context, Func<SyntaxNode, bool> isAttributeSytnax)
        {
            IArrayCreationOperation arrayCreationExpression = (IArrayCreationOperation)context.Operation;

            // We can't replace array allocations in attributes, as they're persisted to metadata
            // TODO: Once we have operation walkers, we can replace this syntactic check with an operation-based check.
            if (arrayCreationExpression.Syntax.Ancestors().Any(isAttributeSytnax))
            {
                return;
            }

            var linqExpressionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);
            if (arrayCreationExpression.IsWithinExpressionTree(linqExpressionType))
            {
                return;
            }

            if (arrayCreationExpression.DimensionSizes.Length == 1)
            {
                IOperation dimensionSize = arrayCreationExpression.DimensionSizes[0];

                if (dimensionSize.HasConstantValue(0))
                {
                    // Workaround for https://github.com/dotnet/roslyn/issues/10214
                    // Bail out for compiler generated params array creation.
                    if (IsCompilerGeneratedParamsArray(arrayCreationExpression, context))
                    {
                        return;
                    }

                    // pointers can't be used as generic arguments
                    var elementType = arrayCreationExpression.GetElementType();
                    if (elementType == null)
                    {
                        return;
                    }

                    if (elementType.TypeKind != TypeKind.Pointer)
                    {
                        var arrayType = context.Compilation.GetOrCreateTypeByMetadataName(ArrayTypeName);
                        if (arrayType == null)
                        {
                            return;
                        }

                        IMethodSymbol emptyMethod = (IMethodSymbol)arrayType.GetMembers(ArrayEmptyMethodName).First();
                        var constructed = emptyMethod.Construct(elementType);

                        string typeName = constructed.ToDisplayString(ReportFormat);
                        context.ReportDiagnostic(arrayCreationExpression.Syntax.CreateDiagnostic(UseArrayEmptyDescriptor, typeName));
                    }
                }
            }
        }

        private static bool IsCompilerGeneratedParamsArray(IArrayCreationOperation arrayCreationExpression, OperationAnalysisContext context)
        {
            var model = arrayCreationExpression.SemanticModel;

            // Compiler generated array creation seems to just use the syntax from the parent.
            var parent = model.GetOperation(arrayCreationExpression.Syntax, context.CancellationToken);
            if (parent == null)
            {
                return false;
            }

            ISymbol? targetSymbol = null;
            var arguments = ImmutableArray<IArgumentOperation>.Empty;
            if (parent is IInvocationOperation invocation)
            {
                targetSymbol = invocation.TargetMethod;
                arguments = invocation.Arguments;
            }
            else
            {
                if (parent is IObjectCreationOperation objectCreation)
                {
                    targetSymbol = objectCreation.Constructor;
                    arguments = objectCreation.Arguments;
                }
                else if (parent is IPropertyReferenceOperation propertyReference)
                {
                    targetSymbol = propertyReference.Property;
                    arguments = propertyReference.Arguments;
                }
            }

            if (targetSymbol == null)
            {
                return false;
            }

            var parameters = targetSymbol.GetParameters();
            if (parameters.IsEmpty || !parameters[^1].IsParams)
            {
                return false;
            }

            // At this point the array creation is known to be compiler synthesized as part of a call
            // to a method with a params parameter, and so it is probably sound to return true at this point.
            // As a sanity check, verify that the last argument to the call is equivalent to the array creation.
            // (Comparing for object identity does not work because the semantic model can return a fresh operation tree.)
            var lastArgument = arguments.LastOrDefault();
            return lastArgument != null && lastArgument.Value.Syntax == arrayCreationExpression.Syntax && AreEquivalentZeroLengthArrayCreations(arrayCreationExpression, lastArgument.Value as IArrayCreationOperation);
        }

        private static bool AreEquivalentZeroLengthArrayCreations(IArrayCreationOperation? first, IArrayCreationOperation? second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            ImmutableArray<IOperation> sizes = first.DimensionSizes;
            if (sizes.Length != 1 || !sizes[0].HasConstantValue(0))
            {
                return false;
            }

            sizes = second.DimensionSizes;
            if (sizes.Length != 1 || !sizes[0].HasConstantValue(0))
            {
                return false;
            }

            return first.Type.Equals(second.Type);
        }

        protected abstract bool IsAttributeSyntax(SyntaxNode node);
    }
}
