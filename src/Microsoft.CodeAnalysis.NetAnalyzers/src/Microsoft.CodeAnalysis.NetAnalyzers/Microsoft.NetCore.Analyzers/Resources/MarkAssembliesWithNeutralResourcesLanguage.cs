// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1824: <inheritdoc cref="MarkAssembliesWithNeutralResourcesLanguageTitle"/>
    /// </summary>
    public abstract class MarkAssembliesWithNeutralResourcesLanguageAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1824";

        protected const string GeneratedCodeAttribute = "GeneratedCodeAttribute";
        protected const string StronglyTypedResourceBuilder = "StronglyTypedResourceBuilder";
        private const string Designer = ".Designer.";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(MarkAssembliesWithNeutralResourcesLanguageTitle)),
            CreateLocalizableResourceString(nameof(MarkAssembliesWithNeutralResourcesLanguageMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(MarkAssembliesWithNeutralResourcesLanguageDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isReportedAtCompilationEnd: true);

        protected abstract void RegisterAttributeAnalyzer(CompilationStartAnalysisContext context, Action onResourceFound, INamedTypeSymbol generatedCode);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            // this analyzer is safe from running concurrently.
            context.EnableConcurrentExecution();

            // set generated file mode to analyze since I only analyze generated files and doesn't report
            // any diagnostics from it.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCodeDomCompilerGeneratedCodeAttribute, out var generatedCode))
                {
                    return;
                }

                INamedTypeSymbol? attribute = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemResourcesNeutralResourcesLanguageAttribute);
                if (attribute is null)
                {
                    return;
                }

                if (TryCheckNeutralResourcesLanguageAttribute(context.Compilation, attribute, out var data))
                {
                    // attribute already exist
                    return;
                }

                var hasResource = false;
                RegisterAttributeAnalyzer(context, () => hasResource = true, generatedCode);

                context.RegisterCompilationEndAction(context =>
                {
                    // there is nothing to do.
                    if (!hasResource)
                    {
                        return;
                    }

                    if (data != null &&
                        data.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is { } attributeSyntax)
                    {
                        // we have the attribute but its doing it wrong.
                        context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(Rule));
                        return;
                    }

                    // attribute just don't exist
                    context.ReportNoLocationDiagnostic(Rule);
                });
            });
        }

        protected static bool CheckDesignerFile(SyntaxTree tree)
        {
            return tree.FilePath?.IndexOf(Designer, StringComparison.OrdinalIgnoreCase) > 0;
        }

        protected static bool CheckResxGeneratedFile(SemanticModel model, SyntaxNode attribute, SyntaxNode? argument, INamedTypeSymbol generatedCode, CancellationToken cancellationToken)
        {
            if (!CheckDesignerFile(model.SyntaxTree) || argument == null)
            {
                return false;
            }

            if (model.GetSymbolInfo(attribute, cancellationToken).Symbol?.ContainingType?.Equals(generatedCode) != true)
            {
                return false;
            }

            Optional<object?> constValue = model.GetConstantValue(argument, cancellationToken);
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
            IEnumerable<AttributeData> attributes = compilation.Assembly.GetAttributes(attribute);
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