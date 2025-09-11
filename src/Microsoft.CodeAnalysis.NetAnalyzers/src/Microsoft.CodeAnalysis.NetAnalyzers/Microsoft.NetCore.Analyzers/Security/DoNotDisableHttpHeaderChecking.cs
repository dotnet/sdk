// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5365: <inheritdoc cref="DoNotDisableHTTPHeaderChecking"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDisableHTTPHeaderChecking : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5365";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(DoNotDisableHTTPHeaderChecking)),
            CreateLocalizableResourceString(nameof(DoNotDisableHTTPHeaderCheckingMessage)),
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(DoNotDisableHTTPHeaderCheckingDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var compilation = compilationStartAnalysisContext.Compilation;
                var httpRuntimeSectionTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebConfigurationHttpRuntimeSection);

                if (httpRuntimeSectionTypeSymbol == null)
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var simpleAssignmentOperation = (ISimpleAssignmentOperation)operationAnalysisContext.Operation;

                    if (simpleAssignmentOperation.Target is IPropertyReferenceOperation propertyReferenceOperation)
                    {
                        var property = propertyReferenceOperation.Property;

                        if (property.Name == "EnableHeaderChecking" &&
                            property.ContainingType.Equals(httpRuntimeSectionTypeSymbol) &&
                            simpleAssignmentOperation.Value.ConstantValue.HasValue &&
                            simpleAssignmentOperation.Value.ConstantValue.Value is false)
                        {
                            operationAnalysisContext.ReportDiagnostic(
                                    simpleAssignmentOperation.CreateDiagnostic(
                                        Rule));
                        }
                    }
                }, OperationKind.SimpleAssignment);
            });
        }
    }
}
