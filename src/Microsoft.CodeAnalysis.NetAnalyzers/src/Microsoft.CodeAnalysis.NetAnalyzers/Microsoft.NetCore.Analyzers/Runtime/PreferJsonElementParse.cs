// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2026: Prefer JsonElement.Parse over JsonDocument.Parse().RootElement
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferJsonElementParse : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2026";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferJsonElementParseTitle)),
            CreateLocalizableResourceString(nameof(PreferJsonElementParseMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferJsonElementParseDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                // Get the JsonDocument and JsonElement types
                INamedTypeSymbol? jsonDocumentType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextJsonJsonDocument);
                INamedTypeSymbol? jsonElementType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextJsonJsonElement);

                if (jsonDocumentType is null || jsonElementType is null)
                {
                    return;
                }

                // Check if JsonElement.Parse and JsonDocument.Parse exist
                if (!jsonElementType.GetMembers("Parse").Any(m => m is IMethodSymbol { IsStatic: true }) ||
                    !jsonDocumentType.GetMembers("Parse").Any(m => m is IMethodSymbol { IsStatic: true }))
                {
                    return;
                }

                // Get the RootElement property
                IPropertySymbol? rootElementProperty = jsonDocumentType.GetMembers("RootElement")
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault();

                if (rootElementProperty is null)
                {
                    return;
                }

                // Get all JsonElement.Parse overloads for matching
                var jsonElementParseOverloads = jsonElementType.GetMembers("Parse")
                    .OfType<IMethodSymbol>()
                    .Where(m => m.IsStatic)
                    .ToImmutableArray();

                context.RegisterOperationAction(context =>
                {
                    var propertyReference = (IPropertyReferenceOperation)context.Operation;

                    // Check if this is accessing the RootElement property
                    if (!SymbolEqualityComparer.Default.Equals(propertyReference.Property, rootElementProperty))
                    {
                        return;
                    }

                    // Check if the instance is a direct call to JsonDocument.Parse
                    if (propertyReference.Instance is not IInvocationOperation invocation)
                    {
                        return;
                    }

                    if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, jsonDocumentType))
                    {
                        return;
                    }

                    if (invocation.TargetMethod.Name != "Parse")
                    {
                        return;
                    }

                    // Now we have the pattern: JsonDocument.Parse(...).RootElement
                    // Check if there's a matching JsonElement.Parse overload with the same parameter types
                    var jsonDocumentParseMethod = invocation.TargetMethod;
                    bool hasMatchingOverload = jsonElementParseOverloads.Any(elementParse =>
                        elementParse.Parameters.Length == jsonDocumentParseMethod.Parameters.Length &&
                        elementParse.Parameters.Zip(jsonDocumentParseMethod.Parameters, (p1, p2) =>
                            SymbolEqualityComparer.Default.Equals(p1.Type, p2.Type)).All(match => match));

                    if (hasMatchingOverload)
                    {
                        context.ReportDiagnostic(propertyReference.CreateDiagnostic(Rule));
                    }
                }, OperationKind.PropertyReference);
            });
        }
    }
}
