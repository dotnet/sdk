// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2211: <inheritdoc cref="NonConstantFieldsShouldNotBeVisibleTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class NonConstantFieldsShouldNotBeVisibleAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2211";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(NonConstantFieldsShouldNotBeVisibleTitle)),
            CreateLocalizableResourceString(nameof(NonConstantFieldsShouldNotBeVisibleMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(NonConstantFieldsShouldNotBeVisibleDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(obj =>
            {
                var field = (IFieldSymbol)obj.Symbol;

                // Only report diagnostic on externally visible non-readonly static fields
                if (field.IsExternallyVisible() && !field.IsConst && field.IsStatic && !field.IsReadOnly)
                {
                    obj.ReportDiagnostic(field.CreateDiagnostic(Rule));
                }
            }, SymbolKind.Field);
        }
    }
}
