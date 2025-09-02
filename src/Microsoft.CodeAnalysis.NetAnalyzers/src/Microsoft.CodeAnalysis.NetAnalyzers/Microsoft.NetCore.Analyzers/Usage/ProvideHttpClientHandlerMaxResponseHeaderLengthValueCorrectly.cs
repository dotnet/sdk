// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Usage
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2262: <inheritdoc cref="ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectlyTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectly : DiagnosticAnalyzer
    {
        private const string MaxResponseHeadersLengthPropertyName = "MaxResponseHeadersLength";
        private const int MaxLimitToReport = 128;
        internal const string RuleId = "CA2262";

        internal static readonly DiagnosticDescriptor EnsureMaxResponseHeaderLengthRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectlyTitle)),
            CreateLocalizableResourceString(nameof(ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectlyMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(ProvideHttpClientHandlerMaxResponseHeaderLengthValueCorrectlyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(EnsureMaxResponseHeaderLengthRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var httpClientHandlerPropSymbol = context.Compilation
                                    .GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNetHttpHttpClientHandler)
                                    ?.GetMembers(MaxResponseHeadersLengthPropertyName)
                                    .FirstOrDefault();

                var socketClientHandlerPropSymbol = context.Compilation
                                    .GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNetHttpSocketsHttpHandler)
                                    ?.GetMembers(MaxResponseHeadersLengthPropertyName)
                                    .FirstOrDefault();

                if (httpClientHandlerPropSymbol is null || socketClientHandlerPropSymbol is null)
                {
                    return;
                }

                ImmutableArray<ISymbol> symbols = ImmutableArray.Create(httpClientHandlerPropSymbol, socketClientHandlerPropSymbol);
                context.RegisterOperationAction(context => AnalyzeSimpleAssignmentOperationAndCreateDiagnostic(context, symbols), OperationKind.SimpleAssignment);
            });
        }

        private static void AnalyzeSimpleAssignmentOperationAndCreateDiagnostic(OperationAnalysisContext context, ImmutableArray<ISymbol> propSymbols)
        {
            var assignmentOperation = (ISimpleAssignmentOperation)context.Operation;

            if (!IsValidPropertyAssignmentOperation(assignmentOperation, propSymbols))
            {
                return;
            }

            if (assignmentOperation.Value is null || !assignmentOperation.Value.ConstantValue.HasValue || assignmentOperation.Value.ConstantValue.Value is not int propertyValue)
            {
                return;
            }

            // If the user set the value to int.MaxValue, their intention is to disable the limit, and we shouldn't emit a warning.
            if (propertyValue is > MaxLimitToReport and not int.MaxValue)
            {
                context.ReportDiagnostic(context.Operation.CreateDiagnostic(EnsureMaxResponseHeaderLengthRule, propertyValue));
            }
        }

        private static bool IsValidPropertyAssignmentOperation(ISimpleAssignmentOperation operation, ImmutableArray<ISymbol> propSymbols)
        {
            if (operation.Target is not IPropertyReferenceOperation propertyReferenceOperation)
            {
                return false;
            }

            if (!propSymbols.Contains(propertyReferenceOperation.Member))
            {
                return false;
            }

            return operation.Value is IFieldReferenceOperation or ILiteralOperation or IBinaryOperation;
        }
    }
}