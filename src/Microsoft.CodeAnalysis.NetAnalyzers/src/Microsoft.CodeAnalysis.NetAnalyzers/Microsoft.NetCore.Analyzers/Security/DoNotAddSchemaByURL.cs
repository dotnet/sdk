// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    /// CA3061: <inheritdoc cref="DoNotAddSchemaByURL"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotAddSchemaByURL : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA3061";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(DoNotAddSchemaByURL)),
            CreateLocalizableResourceString(nameof(DoNotAddSchemaByURLMessage)),
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(DoNotAddSchemaByURLDescription)),
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
                var xmlSchemaCollectionTypeSymbol = compilationStartAnalysisContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlSchemaXmlSchemaCollection);

                if (xmlSchemaCollectionTypeSymbol == null)
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var methodSymbol = invocationOperation.TargetMethod;

                    if (methodSymbol.ContainingType.Equals(xmlSchemaCollectionTypeSymbol) &&
                        methodSymbol.Name == "Add" &&
                        methodSymbol.Parameters.Length > 1 &&
                        methodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_String)
                    {
                        operationAnalysisContext.ReportDiagnostic(
                            invocationOperation.CreateDiagnostic(
                                Rule));
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
