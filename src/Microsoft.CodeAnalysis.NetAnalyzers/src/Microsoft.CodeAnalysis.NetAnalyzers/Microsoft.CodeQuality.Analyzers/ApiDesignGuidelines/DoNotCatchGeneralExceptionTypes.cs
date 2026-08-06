// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1031: Do not catch general exception types
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed class DoNotCatchGeneralExceptionTypesAnalyzer : DoNotCatchGeneralUnlessRethrownAnalyzer
    {
        internal const string RuleId = "CA1031";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotCatchGeneralExceptionTypesTitle)),
            CreateLocalizableResourceString(nameof(DoNotCatchGeneralExceptionTypesMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotCatchGeneralExceptionTypesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public DoNotCatchGeneralExceptionTypesAnalyzer() : base(shouldCheckLambdas: true, allowExcludedSymbolNames: true)
        {
        }

        protected override Diagnostic CreateDiagnostic(IMethodSymbol containingMethod, SyntaxToken catchKeyword)
        {
            return catchKeyword.CreateDiagnostic(Rule, containingMethod.Name);
        }

        protected override bool IsConfiguredDisallowedExceptionType(INamedTypeSymbol namedTypeSymbol, IMethodSymbol containingMethod, Compilation compilation, AnalyzerOptions analyzerOptions, CancellationToken cancellationToken)
        {
            if (base.IsConfiguredDisallowedExceptionType(namedTypeSymbol, containingMethod, compilation, analyzerOptions, cancellationToken))
            {
                return true;
            }

            var symbolNamesOption = analyzerOptions.GetDisallowedSymbolNamesWithValueOption(Rule, containingMethod, compilation);
            return symbolNamesOption.Contains(namedTypeSymbol);
        }
    }
}
