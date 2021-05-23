// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Resources
{
    /// <summary>
    /// CA1824: Mark assemblies with NeutralResourcesLanguageAttribute
    /// </summary>
    public abstract class MarkAssembliesWithNeutralResourcesLanguageAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1824";

        protected const string GeneratedCodeAttribute = "GeneratedCodeAttribute";
        protected const string StronglyTypedResourceBuilder = "StronglyTypedResourceBuilder";
        private const string Designer = ".Designer.";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkAssembliesWithNeutralResourcesLanguageTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkAssembliesWithNeutralResourcesLanguageMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MarkAssembliesWithNeutralResourcesLanguageDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isReportedAtCompilationEnd: true);

        protected abstract void RegisterAttributeAnalyzer(CompilationStartAnalysisContext context, Action onResourceFound, INamedTypeSymbol generatedCode);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // this analyzer is safe from running concurrently.
            context.EnableConcurrentExecution();

            // set generated file mode to analyze since I only analyze generated files and doesn't report
            // any diagnostics from it.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

            context.RegisterCompilationStartAction(cc =>
            {
                INamedTypeSymbol? generatedCode = cc.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCodeDomCompilerGeneratedCodeAttribute);
                if (generatedCode is null)
                {
                    return;
                }

                INamedTypeSymbol? attribute = cc.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemResourcesNeutralResourcesLanguageAttribute);
                if (attribute is null)
                {
                    return;
                }

                if (TryCheckNeutralResourcesLanguageAttribute(cc.Compilation, attribute, out var data))
                {
                    // attribute already exist
                    return;
                }

                var hasResource = false;
                RegisterAttributeAnalyzer(cc, () => hasResource = true, generatedCode);

                cc.RegisterCompilationEndAction(ce =>
                {
                    // there is nothing to do.
                    if (!hasResource)
                    {
                        return;
                    }

                    if (data != null)
                    {
                        // we have the attribute but its doing it wrong.
                        ce.ReportDiagnostic(data.ApplicationSyntaxReference.GetSyntax(ce.CancellationToken).CreateDiagnostic(Rule));
                        return;
                    }

                    // attribute just don't exist
                    ce.ReportNoLocationDiagnostic(Rule);
                });
            });
        }

        protected static bool CheckDesignerFile(SyntaxTree tree)
        {
            return tree.FilePath?.IndexOf(Designer, StringComparison.OrdinalIgnoreCase) > 0;
        }

        protected static bool CheckResxGeneratedFile(SemanticModel model, SyntaxNode attribute, SyntaxNode argument, INamedTypeSymbol generatedCode, CancellationToken cancellationToken)
        {
            if (!CheckDesignerFile(model.SyntaxTree))
            {
                return false;
            }

            if (model.GetSymbolInfo(attribute, cancellationToken).Symbol?.ContainingType?.Equals(generatedCode) != true)
            {
                return false;
            }

            Optional<object> constValue = model.GetConstantValue(argument, cancellationToken);
            if (!constValue.HasValue)
            {
                return false;
            }

            if (constValue.Value is not string stringValue)
            {
                return false;
            }

            if (stringValue.IndexOf(StronglyTypedResourceBuilder, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return true;
        }

        private static bool TryCheckNeutralResourcesLanguageAttribute(Compilation compilation, INamedTypeSymbol attribute, out AttributeData? attributeData)
        {
            IEnumerable<AttributeData> attributes = compilation.Assembly.GetAttributes().Where(d => SymbolEqualityComparer.Default.Equals(attribute, d.AttributeClass));
            foreach (AttributeData data in attributes)
            {
                if (data.ConstructorArguments.Any(c => c.Value is string constantValue && !string.IsNullOrWhiteSpace(constantValue)))
                {
                    // found one that already does right thing.
                    attributeData = data;
                    return true;
                }
            }

            // either we couldn't find one or existing one is wrong.
            attributeData = attributes.FirstOrDefault();
            return false;
        }
    }
}