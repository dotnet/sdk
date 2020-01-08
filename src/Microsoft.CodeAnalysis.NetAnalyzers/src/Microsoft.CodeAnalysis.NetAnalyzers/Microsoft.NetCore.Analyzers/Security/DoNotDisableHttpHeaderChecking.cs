// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDisableHTTPHeaderChecking : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5365";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableHTTPHeaderChecking),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableHTTPHeaderCheckingMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableHTTPHeaderCheckingDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                DiagnosticHelpers.DefaultDiagnosticSeverity,
                isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                description: s_Description,
                helpLinkUri: null,
                customTags: WellKnownDiagnosticTags.Telemetry);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
                            simpleAssignmentOperation.Value.ConstantValue.Value.Equals(false))
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
