// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1041: Provide ObsoleteAttribute message
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ProvideObsoleteAttributeMessageAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1041";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ProvideObsoleteAttributeMessageTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ProvideObsoleteAttributeMessageMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.ProvideObsoleteAttributeMessageDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? obsoleteAttributeType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);
                if (obsoleteAttributeType == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(sc => AnalyzeSymbol(sc, obsoleteAttributeType),
                    SymbolKind.NamedType,
                    SymbolKind.Method,
                    SymbolKind.Field,
                    SymbolKind.Property,
                    SymbolKind.Event);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol obsoleteAttributeType)
        {
            // FxCop compat: only analyze externally visible symbols by default
            if (!context.Options.MatchesConfiguredVisibility(Rule, context.Symbol, context.Compilation))
            {
                return;
            }

            ImmutableArray<AttributeData> attributes = context.Symbol.GetAttributes();
            foreach (AttributeData attribute in attributes)
            {
                if (attribute.AttributeClass.Equals(obsoleteAttributeType))
                {
                    // ObsoleteAttribute has a constructor that takes no params and two
                    // other constructors that take a message as the first param.
                    // If there are no arguments specified or if the message argument is empty
                    // then report a diagnostic.
                    if (attribute.ConstructorArguments.IsEmpty ||
                        string.IsNullOrEmpty(attribute.ConstructorArguments.First().Value as string))
                    {
                        SyntaxNode node = attribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
                        context.ReportDiagnostic(node.CreateDiagnostic(Rule, context.Symbol.Name));
                    }
                }
            }
        }
    }
}