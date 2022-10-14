// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2109: <inheritdoc cref="ReviewVisibleEventHandlersTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ReviewVisibleEventHandlersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2109";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ReviewVisibleEventHandlersTitle)),
            CreateLocalizableResourceString(nameof(ReviewVisibleEventHandlersMessageDefault)),
            DiagnosticCategory.Security,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(ReviewVisibleEventHandlersDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var eventArgsType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);

                context.RegisterSymbolAction(context =>
                {
                    var method = (IMethodSymbol)context.Symbol;

                    if (!method.IsExternallyVisible())
                    {
                        return;
                    }

                    if (!method.HasEventHandlerSignature(eventArgsType))
                    {
                        return;
                    }

                    if (method.IsOverride || method.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    context.ReportDiagnostic(method.CreateDiagnostic(Rule, method.Name));
                }, SymbolKind.Method);
            });
        }
    }
}