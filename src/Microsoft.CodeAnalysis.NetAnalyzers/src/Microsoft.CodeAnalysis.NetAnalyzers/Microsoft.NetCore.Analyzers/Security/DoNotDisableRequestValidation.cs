// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDisableRequestValidation : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5363";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableRequestValidation),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableRequestValidationMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotDisableRequestValidationDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_Description,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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

                            var attr = symbol.GetAttributes().FirstOrDefault(s => s.AttributeClass.Equals(validateInputAttributeTypeSymbol));

                            // If the method doesn't have the ValidateInput attribute, check its type.
                            if (attr == null)
                            {
                                symbol = typeSymbol;
                                attr = symbol.GetAttributes().FirstOrDefault(s => s.AttributeClass.Equals(validateInputAttributeTypeSymbol));
                            }

                            // By default, request validation is enabled.
                            if (attr == null)
                            {
                                return;
                            }

                            var constructorArguments = attr.ConstructorArguments;

                            if (constructorArguments.Length == 1 &&
                                constructorArguments[0].Kind == TypedConstantKind.Primitive &&
                                constructorArguments[0].Value.Equals(false))
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
