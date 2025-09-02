// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// Prefer using 'IsEmpty' or comparing 'Count' / 'Length' property to 0 rather than using 'Any()', both for clarity and for performance.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferLengthCountIsEmptyOverAnyAnalyzer : DiagnosticAnalyzer
    {
        private const string AnyText = nameof(Enumerable.Any);

        internal const string IsEmptyText = nameof(ImmutableArray<dynamic>.IsEmpty);
        internal const string LengthText = nameof(Array.Length);
        internal const string CountText = nameof(ICollection.Count);

        internal const string RuleId = "CA1860";
        internal const string DiagnosticPropertyKey = nameof(DiagnosticPropertyKey);

        internal static readonly DiagnosticDescriptor IsEmptyDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyTitle)),
            CreateLocalizableResourceString(nameof(PreferIsEmptyOverAnyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false
        );

        internal static readonly DiagnosticDescriptor LengthDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyTitle)),
            CreateLocalizableResourceString(nameof(PreferLengthOverAnyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false
        );

        internal static readonly DiagnosticDescriptor CountDescriptor = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyTitle)),
            CreateLocalizableResourceString(nameof(PreferCountOverAnyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferLengthCountIsEmptyOverAnyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            LengthDescriptor,
            CountDescriptor,
            IsEmptyDescriptor
        );

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(ctx =>
            {
                var typeProvider = WellKnownTypeProvider.GetOrCreate(ctx.Compilation);
                var iEnumerable = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerable);
                var iEnumerableOfT = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerable1);
                var linqExpressionType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);
                var anyMethod = typeProvider
                    .GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable)
                    ?.GetMembers(AnyText)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsExtensionMethod && m.Parameters.Length == 1);
                if (iEnumerable is not null && iEnumerableOfT is not null && anyMethod is not null)
                {
                    ctx.RegisterOperationAction(c => OnInvocationAnalysis(c, iEnumerable, iEnumerableOfT, anyMethod, linqExpressionType), OperationKind.Invocation);
                }
            });
        }

        private static void OnInvocationAnalysis(OperationAnalysisContext context, INamedTypeSymbol iEnumerable, INamedTypeSymbol iEnumerableOfT, IMethodSymbol anyMethod, INamedTypeSymbol? linqExpressionType)
        {
            var invocation = (IInvocationOperation)context.Operation;
            if (invocation.IsWithinExpressionTree(linqExpressionType))
            {
                return;
            }

            var originalMethod = invocation.TargetMethod.OriginalDefinition;
            if (originalMethod.MethodKind == MethodKind.ReducedExtension)
            {
                originalMethod = originalMethod.ReducedFrom!;
            }

            if (originalMethod.Equals(anyMethod, SymbolEqualityComparer.Default))
            {
                var type = invocation.GetReceiverType(context.Compilation, beforeConversion: true, context.CancellationToken);
                if (type is null || (!type.AllInterfaces.Contains(iEnumerable, SymbolEqualityComparer.Default) && !type.AllInterfaces.Contains(iEnumerableOfT)))
                {
                    return;
                }

                if (HasEligibleIsEmptyProperty(type))
                {
                    var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add(DiagnosticPropertyKey, IsEmptyText);
                    context.ReportDiagnostic(invocation.CreateDiagnostic(IsEmptyDescriptor, properties: properties.ToImmutable()));
                }
                else if (HasEligibleLengthProperty(type))
                {
                    var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add(DiagnosticPropertyKey, LengthText);
                    context.ReportDiagnostic(invocation.CreateDiagnostic(LengthDescriptor, properties: properties.ToImmutable()));
                }

                else if (HasEligibleCountProperty(type))
                {
                    var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add(DiagnosticPropertyKey, CountText);
                    context.ReportDiagnostic(invocation.CreateDiagnostic(CountDescriptor, properties: properties.ToImmutable()));
                }
            }
        }

        private static bool HasEligibleIsEmptyProperty(ITypeSymbol typeSymbol)
        {
            return typeSymbol.GetMembers(IsEmptyText)
                .OfType<IPropertySymbol>()
                .Any(property => property.Type.SpecialType == SpecialType.System_Boolean);
        }

        private static bool HasEligibleLengthProperty(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is IArrayTypeSymbol)
            {
                return true;
            }

            return typeSymbol.GetMembers(LengthText)
                .OfType<IPropertySymbol>()
                .Any(property => property.Type.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32);
        }

        private static bool HasEligibleCountProperty(ITypeSymbol typeSymbol)
        {
            return typeSymbol.GetMembers(CountText)
                .OfType<IPropertySymbol>()
                .Any(property => property.Type.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32);
        }
    }
}