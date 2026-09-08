// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1825: <inheritdoc cref="AvoidZeroLengthArrayAllocationsTitle"/>
    /// Base type for an analyzer that looks for empty array allocations and recommends their replacement.
    /// </summary>
    public abstract class AvoidZeroLengthArrayAllocationsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1825";

        /// <summary>The name of the Empty method on System.Array.</summary>
        internal const string ArrayEmptyMethodName = "Empty";

        private static readonly SymbolDisplayFormat ReportFormat = new(
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        /// <summary>The diagnostic descriptor used when Array.Empty should be used instead of a new array allocation.</summary>
        internal static readonly DiagnosticDescriptor UseArrayEmptyDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidZeroLengthArrayAllocationsTitle)),
            CreateLocalizableResourceString(nameof(AvoidZeroLengthArrayAllocationsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        /// <summary>Gets the set of supported diagnostic descriptors from this analyzer.</summary>
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(UseArrayEmptyDescriptor);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // When compilation begins, check whether Array.Empty<T> is available.
            // Only if it is, register the array-creation operation action.
            context.RegisterCompilationStartAction(context =>
            {
                INamedTypeSymbol typeSymbol = context.Compilation.GetSpecialType(SpecialType.System_Array);
                if (typeSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    if (typeSymbol.GetMembers(ArrayEmptyMethodName).FirstOrDefault() is IMethodSymbol methodSymbol && methodSymbol.DeclaredAccessibility == Accessibility.Public &&
                        methodSymbol.IsStatic && methodSymbol.Arity == 1 && methodSymbol.Parameters.IsEmpty)
                    {
                        var linqExpressionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);
                        context.RegisterOperationAction(c => AnalyzeOperation(c, methodSymbol, linqExpressionType), OperationKind.ArrayCreation);
                    }
                }
            });
        }

        private static void AnalyzeOperation(OperationAnalysisContext context, IMethodSymbol arrayEmptyMethodSymbol, INamedTypeSymbol? linqExpressionType)
        {
            IArrayCreationOperation arrayCreationExpression = (IArrayCreationOperation)context.Operation;

            // We can't replace array allocations in attributes, as they're persisted to metadata
            if (arrayCreationExpression.GetAncestor<IAttributeOperation>(OperationKind.Attribute) != null)
            {
                return;
            }

            if (arrayCreationExpression.IsWithinExpressionTree(linqExpressionType))
            {
                return;
            }

            if (arrayCreationExpression.DimensionSizes.Length == 1)
            {
                IOperation dimensionSize = arrayCreationExpression.DimensionSizes[0];

                if (dimensionSize.HasConstantValue(0))
                {
                    // Compiler-supplied params arrays have no source array expression to replace.
                    if (arrayCreationExpression.Parent is IArgumentOperation { ArgumentKind: ArgumentKind.ParamArray })
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
                        var constructed = arrayEmptyMethodSymbol.Construct(elementType);

                        string typeName = constructed.ToDisplayString(ReportFormat);
                        context.ReportDiagnostic(arrayCreationExpression.Syntax.CreateDiagnostic(UseArrayEmptyDescriptor, typeName));
                    }
                }
            }
        }
    }
}
