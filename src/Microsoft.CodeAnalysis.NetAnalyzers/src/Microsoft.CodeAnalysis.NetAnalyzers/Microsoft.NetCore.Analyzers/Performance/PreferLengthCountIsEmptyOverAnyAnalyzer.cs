// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Immutable;
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
                var linqExpressionType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);

                var anyMethod = typeProvider
                    .GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable)
                    ?.GetMembers(AnyText)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsExtensionMethod && m.Parameters.Length == 1);

                if (anyMethod is not null)
                {
                    ctx.RegisterOperationAction(c => OnInvocationAnalysis(c, anyMethod, linqExpressionType), OperationKind.Invocation);
                }
            });
        }

        private static void OnInvocationAnalysis(OperationAnalysisContext context, IMethodSymbol anyMethod, INamedTypeSymbol? linqExpressionType)
        {
            var invocation = (IInvocationOperation)context.Operation;

            var originalMethod = invocation.TargetMethod.OriginalDefinition;
            if (originalMethod.MethodKind == MethodKind.ReducedExtension)
            {
                originalMethod = originalMethod.ReducedFrom!;
            }

            if (!originalMethod.Equals(anyMethod, SymbolEqualityComparer.Default) ||
                invocation.IsWithinExpressionTree(linqExpressionType))
            {
                return;
            }

            var type = invocation.GetReceiverType(context.Compilation, beforeConversion: true, context.CancellationToken);

            for(; type != null; type = type.BaseType)
            {
                if (HasEligibleIsEmptyProperty(type))
                {
                    ReportDiagnostic(invocation, IsEmptyDescriptor, IsEmptyText, context);
                }
                else if (HasEligibleSizeProperty(type, CountText))
                {
                    ReportDiagnostic(invocation, CountDescriptor, CountText, context);
                }
                else if (HasEligibleSizeProperty(type, LengthText))
                {
                    ReportDiagnostic(invocation, LengthDescriptor, LengthText, context);
                }
            }
        }

        private static bool IsGenericEnumerable(ITypeSymbol type, ITypeSymbol enumerableOfT)
        {
            foreach (var interfaceType in type.AllInterfaces)
            {
                if (interfaceType.ConstructedFrom == enumerableOfT)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEligibleIsEmptyProperty(ITypeSymbol type)
        {
            var members = type.GetMembers(IsEmptyText);

            return !members.IsEmpty &&
                    members[0] is IPropertySymbol property &&
                    property.Type.SpecialType == SpecialType.System_Boolean;
        }

        private static bool HasEligibleSizeProperty(ITypeSymbol type, string propertyName)
        {
            var members = type.GetMembers(propertyName);

            return !members.IsEmpty &&
                    members[0] is IPropertySymbol property &&
                    property.Type.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32;
        }

        private static void ReportDiagnostic(IInvocationOperation invocation, DiagnosticDescriptor descriptor, string propertyName, OperationAnalysisContext context)
        {
            var properties = ImmutableDictionary<string, string?>.Empty.Add(DiagnosticPropertyKey, propertyName);
            context.ReportDiagnostic(invocation.CreateDiagnostic(descriptor, properties));
        }
    }
}
