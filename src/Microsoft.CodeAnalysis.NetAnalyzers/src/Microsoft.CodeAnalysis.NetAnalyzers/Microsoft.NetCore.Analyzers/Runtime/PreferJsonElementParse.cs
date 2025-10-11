// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
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
                INamedTypeSymbol? jsonDocumentType = context.Compilation.GetOrCreateTypeByMetadataName("System.Text.Json.JsonDocument");
                INamedTypeSymbol? jsonElementType = context.Compilation.GetOrCreateTypeByMetadataName("System.Text.Json.JsonElement");

                if (jsonDocumentType is null || jsonElementType is null)
                {
                    return;
                }

                // Check if JsonElement.Parse exists (available in .NET 10+)
                IMethodSymbol? jsonElementParse = null;
                foreach (var member in jsonElementType.GetMembers("Parse"))
                {
                    if (member is IMethodSymbol method && method.IsStatic)
                    {
                        jsonElementParse = method;
                        break;
                    }
                }

                if (jsonElementParse is null)
                {
                    // JsonElement.Parse doesn't exist, so no need to suggest it
                    return;
                }

                // Get the JsonDocument.Parse methods
                IMethodSymbol? jsonDocumentParseMethod = null;
                foreach (var member in jsonDocumentType.GetMembers("Parse"))
                {
                    if (member is IMethodSymbol method && method.IsStatic)
                    {
                        jsonDocumentParseMethod = method;
                        break;
                    }
                }

                if (jsonDocumentParseMethod is null)
                {
                    return;
                }

                // Get the RootElement property
                IPropertySymbol? rootElementProperty = null;
                foreach (var member in jsonDocumentType.GetMembers("RootElement"))
                {
                    if (member is IPropertySymbol property)
                    {
                        rootElementProperty = property;
                        break;
                    }
                }

                if (rootElementProperty is null)
                {
                    return;
                }

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
                    // Check if the JsonDocument is disposed. We'll look for patterns where it's immediately
                    // accessed and not stored, which is the primary concern.

                    // If the parent operation is an assignment to a variable of type JsonElement,
                    // and the JsonDocument is never stored, this is the problematic pattern.
                    if (IsImmediateUseWithoutDisposal(propertyReference))
                    {
                        context.ReportDiagnostic(propertyReference.CreateDiagnostic(Rule));
                    }
                }, OperationKind.PropertyReference);
            });
        }

        private static bool IsImmediateUseWithoutDisposal(IPropertyReferenceOperation propertyReference)
        {
            // The pattern we're looking for is:
            // JsonElement element = JsonDocument.Parse("json").RootElement;
            // 
            // In this case, the propertyReference is the .RootElement access,
            // and its parent should be something that uses it directly without
            // the JsonDocument being stored or disposed.

            // If we walk up the tree and never find the JsonDocument being stored in a variable
            // or being used in a using statement, then it's not being disposed properly.

            // For simplicity, we'll flag any case where:
            // 1. The property reference is the direct result of JsonDocument.Parse()
            // 2. The result is not part of a using declaration/statement

            IOperation? current = propertyReference.Parent;

            // Walk up to find if this is within a using statement/declaration
            while (current != null)
            {
                if (current is IUsingOperation)
                {
                    // It's within a using statement, so it's being disposed
                    return false;
                }

                // Check for using declaration
                if (current is IUsingDeclarationOperation)
                {
                    // It's a using declaration, so it's being disposed
                    return false;
                }

                current = current.Parent;
            }

            // Not properly disposed
            return true;
        }

        private static bool ContainsOperation(IOperation? parent, IOperation child)
        {
            if (parent == null)
            {
                return false;
            }

            if (parent == child)
            {
                return true;
            }

            foreach (var descendant in parent.DescendantsAndSelf())
            {
                if (descendant == child)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
