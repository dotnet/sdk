// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1813: <inheritdoc cref="AvoidUnsealedAttributesTitle"/>
    /// Seal attribute types for improved performance. Sealing attribute types speeds up performance during reflection on custom attributes.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidUnsealedAttributesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1813";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidUnsealedAttributesTitle)),
            CreateLocalizableResourceString(nameof(AvoidUnsealedAttributesMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(AvoidUnsealedAttributesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? attributeType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttribute);
                if (attributeType == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(context =>
                {
                    AnalyzeSymbol((INamedTypeSymbol)context.Symbol, attributeType, context.ReportDiagnostic);
                },
                SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(INamedTypeSymbol namedType, INamedTypeSymbol attributeType, Action<Diagnostic> addDiagnostic)
        {
            if (namedType.IsAbstract || namedType.IsSealed || !namedType.GetBaseTypesAndThis().Contains(attributeType))
            {
                return;
            }

            // Non-sealed non-abstract attribute type.
            addDiagnostic(namedType.CreateDiagnostic(Rule));
        }
    }
}
