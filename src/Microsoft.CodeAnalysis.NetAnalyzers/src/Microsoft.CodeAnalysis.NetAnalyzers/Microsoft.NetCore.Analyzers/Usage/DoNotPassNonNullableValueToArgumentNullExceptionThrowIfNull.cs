// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull : DiagnosticAnalyzer
    {
        internal const string NonNullableValueRuleId = "CA2264";
        internal const string NullableStructRuleId = "CA1871";

        internal static readonly DiagnosticDescriptor DoNotPassNonNullableValueDiagnostic = DiagnosticDescriptorHelper.Create(
            NonNullableValueRuleId,
            CreateLocalizableResourceString(nameof(DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullTitle)),
            CreateLocalizableResourceString(nameof(DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor DoNotPassNullableStructDiagnostic = DiagnosticDescriptorHelper.Create(
            NullableStructRuleId,
            CreateLocalizableResourceString(nameof(DoNotPassNullableStructToArgumentNullExceptionThrowIfNullTitle)),
            CreateLocalizableResourceString(nameof(DoNotPassNullableStructToArgumentNullExceptionThrowIfNullMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(DoNotPassNullableStructToArgumentNullExceptionThrowIfNullDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(static context =>
            {
                var typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                var throwIfNullMethod = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentNullException)
                    ?.GetMembers("ThrowIfNull")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.Parameters is [{ Type.SpecialType: SpecialType.System_Object }, _]);
                if (throwIfNullMethod is null)
                {
                    return;
                }

                context.RegisterOperationAction(ctx => AnalyzeInvocation(ctx, throwIfNullMethod), OperationKind.Invocation);
            });
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context, IMethodSymbol throwIfNullMethod)
        {
            var invocation = (IInvocationOperation)context.Operation;
            if (invocation.TargetMethod.Equals(throwIfNullMethod, SymbolEqualityComparer.Default))
            {
                if (invocation.Arguments[0].Value.WalkDownConversion().Type.IsNonNullableValueType()
                    || invocation.Arguments[0].Value.WalkDownConversion().Kind is OperationKind.NameOf or OperationKind.ObjectCreation or OperationKind.ObjectOrCollectionInitializer)
                {
                    context.ReportDiagnostic(invocation.CreateDiagnostic(DoNotPassNonNullableValueDiagnostic));
                }

                if (invocation.Arguments[0].Value.WalkDownConversion().Type.IsNullableValueType())
                {
                    context.ReportDiagnostic(invocation.CreateDiagnostic(DoNotPassNullableStructDiagnostic));
                }
            }
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DoNotPassNonNullableValueDiagnostic, DoNotPassNullableStructDiagnostic);
    }
}
