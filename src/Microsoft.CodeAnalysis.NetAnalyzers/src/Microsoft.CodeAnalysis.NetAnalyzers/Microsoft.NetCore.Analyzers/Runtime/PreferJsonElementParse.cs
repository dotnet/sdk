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

                // Get all JsonElement.Parse overloads for matching
                var jsonElementParseOverloads = jsonElementType.GetMembers("Parse")
                    .OfType<IMethodSymbol>()
                    .Where(m => m.IsStatic)
                    .ToImmutableArray();

                // Check if JsonElement.Parse exists
                if (jsonElementParseOverloads.IsEmpty ||
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

                context.RegisterOperationAction(context =>
                {
                    var propertyReference = (IPropertyReferenceOperation)context.Operation;

                    // Check if this is accessing the RootElement property and the instance is a direct call to JsonDocument.Parse
                    if (!SymbolEqualityComparer.Default.Equals(propertyReference.Property, rootElementProperty) ||
                        propertyReference.Instance is not IInvocationOperation invocation ||
                        !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, jsonDocumentType) ||
                        invocation.TargetMethod.Name != "Parse")
                    {
                        return;
                    }

                    // Now we have the pattern: JsonDocument.Parse(...).RootElement
                    // Check if there's a matching JsonElement.Parse overload with the same parameter types
                    var jsonDocumentParseMethod = invocation.TargetMethod;

                    foreach (var elementParseOverload in jsonElementParseOverloads)
                    {
                        if (elementParseOverload.Parameters.Length != jsonDocumentParseMethod.Parameters.Length)
                        {
                            continue;
                        }

                        bool parametersMatch = true;
                        for (int i = 0; i < elementParseOverload.Parameters.Length; i++)
                        {
                            if (!SymbolEqualityComparer.Default.Equals(elementParseOverload.Parameters[i].Type, jsonDocumentParseMethod.Parameters[i].Type))
                            {
                                parametersMatch = false;
                                break;
                            }
                        }

                        if (parametersMatch)
                        {
                            context.ReportDiagnostic(propertyReference.CreateDiagnostic(Rule));
                            break;
                        }
                    }
                }, OperationKind.PropertyReference);
            });
        }
    }
}
