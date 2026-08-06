// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5363: <inheritdoc cref="DoNotDisableRequestValidation"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDisableRequestValidation : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5363";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(DoNotDisableRequestValidation)),
            CreateLocalizableResourceString(nameof(DoNotDisableRequestValidationMessage)),
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(DoNotDisableRequestValidationDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(
                                WellKnownTypeNames.SystemWebMvcValidateInputAttribute,
                                out INamedTypeSymbol? validateInputAttributeTypeSymbol))
                    {
                        return;
                    }

                    compilationStartAnalysisContext.RegisterSymbolAction(
                        (SymbolAnalysisContext symbolAnalysisContext) =>
                        {
                            var symbol = symbolAnalysisContext.Symbol;
                            var typeSymbol = symbol.ContainingType;

                            if (typeSymbol == null)
                            {
                                return;
                            }

                            var attr = symbol.GetAttribute(validateInputAttributeTypeSymbol);

                            // If the method doesn't have the ValidateInput attribute, check its type.
                            if (attr == null)
                            {
                                symbol = typeSymbol;
                                attr = symbol.GetAttribute(validateInputAttributeTypeSymbol);
                            }

                            // By default, request validation is enabled.
                            if (attr == null)
                            {
                                return;
                            }

                            var constructorArguments = attr.ConstructorArguments;

                            if (constructorArguments.Length == 1 &&
                                constructorArguments[0].Kind == TypedConstantKind.Primitive &&
                                constructorArguments[0].Value is false)
                            {
                                symbolAnalysisContext.ReportDiagnostic(
                                    symbol.CreateDiagnostic(
                                        Rule,
                                        symbol.Name));
                            }
                        }, SymbolKind.Method);
                });
        }
    }
}
