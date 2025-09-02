// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA2024: <inheritdoc cref="DoNotUseEndOfStreamInAsyncMethodsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseEndOfStreamInAsyncMethodsAnalyzer : DiagnosticAnalyzer
    {
        private const string RuleId = "CA2024";
        private const string EndOfStream = nameof(EndOfStream);

        private static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseEndOfStreamInAsyncMethodsTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseEndOfStreamInAsyncMethodsMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(DoNotUseEndOfStreamInAsyncMethodsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var streamReaderType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOStreamReader);

            if (streamReaderType is null)
            {
                return;
            }

            var endOfStreamProperty = streamReaderType.GetMembers(EndOfStream)
                .OfType<IPropertySymbol>()
                .FirstOrDefault();

            if (endOfStreamProperty is null)
            {
                return;
            }

            context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);

            void AnalyzePropertyReference(OperationAnalysisContext context)
            {
                var operation = (IPropertyReferenceOperation)context.Operation;

                if (!SymbolEqualityComparer.Default.Equals(endOfStreamProperty, operation.Member))
                {
                    return;
                }

                var containingSymbol = operation.TryGetContainingAnonymousFunctionOrLocalFunction() ?? context.ContainingSymbol;

                if (containingSymbol is IMethodSymbol containingMethod && containingMethod.IsAsync)
                {
                    context.ReportDiagnostic(operation.CreateDiagnostic(Rule, operation.Syntax.ToString()));
                }
            }
        }
    }
}
