// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1303: Do not pass literals as localized parameters
    /// A method passes a string literal as a parameter to a constructor or method in the .NET Framework class library and that string should be localizable.
    /// This warning is raised when a literal string is passed as a value to a parameter or property and one or more of the following cases is true:
    ///   1. The LocalizableAttribute attribute of the parameter or property is set to true.
    ///   2. The parameter or property name contains "Text", "Message", or "Caption".
    ///   3. The name of the string parameter that is passed to a Console.Write or Console.WriteLine method is either "value" or "format".
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotPassLiteralsAsLocalizedParameters : AbstractGlobalizationDiagnosticAnalyzer
    {
        internal const string RuleId = "CA1303";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotPassLiteralsAsLocalizedParametersTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotPassLiteralsAsLocalizedParametersMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotPassLiteralsAsLocalizedParametersDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Globalization,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected override void InitializeWorker(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? localizableStateAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemComponentModelLocalizableAttribute);
            INamedTypeSymbol? conditionalAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsConditionalAttribute);
            INamedTypeSymbol? systemConsoleSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConsole);
            ImmutableHashSet<INamedTypeSymbol> typesToIgnore = GetTypesToIgnore(context.Compilation);

            context.RegisterOperationBlockStartAction(operationBlockStartContext =>
            {
                if (operationBlockStartContext.OwningSymbol is not IMethodSymbol containingMethod ||
                    operationBlockStartContext.Options.IsConfiguredToSkipAnalysis(Rule, containingMethod, operationBlockStartContext.Compilation))
                {
                    return;
                }

                Lazy<DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>?> lazyValueContentResult = new Lazy<DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>?>(
                    valueFactory: ComputeValueContentAnalysisResult, isThreadSafe: false);

                operationBlockStartContext.RegisterOperationAction(operationContext =>
                {
                    var argument = (IArgumentOperation)operationContext.Operation;
                    IMethodSymbol? targetMethod = null;
                    switch (argument.Parent)
                    {
                        case IInvocationOperation invocation:
                            targetMethod = invocation.TargetMethod;
                            break;

                        case IObjectCreationOperation objectCreation:
                            targetMethod = objectCreation.Constructor;
                            break;
                    }

                    if (ShouldAnalyze(targetMethod))
                    {
                        AnalyzeArgument(argument.Parameter, containingPropertySymbol: null, operation: argument, reportDiagnostic: operationContext.ReportDiagnostic, GetUseNamingHeuristicOption(operationContext));
                    }
                }, OperationKind.Argument);

                operationBlockStartContext.RegisterOperationAction(operationContext =>
                {
                    var propertyReference = (IPropertyReferenceOperation)operationContext.Operation;
                    if (propertyReference.Parent is IAssignmentOperation assignment &&
                        assignment.Target == propertyReference &&
                        !propertyReference.Property.IsIndexer &&
                        propertyReference.Property.SetMethod?.Parameters.Length == 1 &&
                        ShouldAnalyze(propertyReference.Property))
                    {
                        IParameterSymbol valueSetterParam = propertyReference.Property.SetMethod.Parameters[0];
                        AnalyzeArgument(valueSetterParam, propertyReference.Property, assignment, operationContext.ReportDiagnostic, GetUseNamingHeuristicOption(operationContext));
                    }
                }, OperationKind.PropertyReference);

                return;

                // Local functions
                bool ShouldAnalyze(ISymbol? symbol)
                    => symbol != null && !operationBlockStartContext.Options.IsConfiguredToSkipAnalysis(Rule, symbol, operationBlockStartContext.OwningSymbol, operationBlockStartContext.Compilation);

                static bool GetUseNamingHeuristicOption(OperationAnalysisContext operationContext)
                    => operationContext.Options.GetBoolOptionValue(EditorConfigOptionNames.UseNamingHeuristic, Rule,
                        operationContext.Operation.Syntax.SyntaxTree, operationContext.Compilation, defaultValue: false);

                void AnalyzeArgument(IParameterSymbol parameter, IPropertySymbol? containingPropertySymbol, IOperation operation, Action<Diagnostic> reportDiagnostic, bool useNamingHeuristic)
                {
                    if (ShouldBeLocalized(parameter.OriginalDefinition, containingPropertySymbol?.OriginalDefinition, localizableStateAttributeSymbol, conditionalAttributeSymbol, systemConsoleSymbol, typesToIgnore, useNamingHeuristic) &&
                        lazyValueContentResult.Value != null)
                    {
                        ValueContentAbstractValue stringContentValue = lazyValueContentResult.Value[operation.Kind, operation.Syntax];
                        if (stringContentValue.IsLiteralState)
                        {
                            Debug.Assert(!stringContentValue.LiteralValues.IsEmpty);

                            if (stringContentValue.LiteralValues.Any(l => l is not string))
                            {
                                return;
                            }

                            var stringLiteralValues = stringContentValue.LiteralValues.Cast<string?>();

                            // FxCop compat: Do not fire if the literal value came from a default parameter value
                            if (stringContentValue.LiteralValues.Count == 1 &&
                                parameter.IsOptional &&
                                parameter.ExplicitDefaultValue is string defaultValue &&
                                defaultValue == stringLiteralValues.Single())
                            {
                                return;
                            }

                            // FxCop compat: Do not fire if none of the string literals have any non-control character.
                            if (!LiteralValuesHaveNonControlCharacters(stringLiteralValues))
                            {
                                return;
                            }

                            // FxCop compat: Filter out xml string literals.
                            IEnumerable<string> filteredStrings = stringLiteralValues.Where(literal => literal != null && !LooksLikeXmlTag(literal))!;
                            if (filteredStrings.Any())
                            {
                                // Method '{0}' passes a literal string as parameter '{1}' of a call to '{2}'. Retrieve the following string(s) from a resource table instead: "{3}".
                                var arg1 = containingMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                                var arg2 = parameter.Name;
                                var arg3 = parameter.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                                var arg4 = FormatLiteralValues(filteredStrings);
                                var diagnostic = operation.CreateDiagnostic(Rule, arg1, arg2, arg3, arg4);
                                reportDiagnostic(diagnostic);
                            }
                        }
                    }
                }

                DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>? ComputeValueContentAnalysisResult()
                {
                    var cfg = operationBlockStartContext.OperationBlocks.GetControlFlowGraph();
                    if (cfg != null)
                    {
                        var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(operationBlockStartContext.Compilation);
                        return ValueContentAnalysis.TryGetOrComputeResult(cfg, containingMethod, wellKnownTypeProvider,
                            operationBlockStartContext.Options, Rule, PointsToAnalysisKind.PartialWithoutTrackingFieldsAndProperties);
                    }

                    return null;
                }
            });
        }

        private static ImmutableHashSet<INamedTypeSymbol> GetTypesToIgnore(Compilation compilation)
        {
            var builder = PooledHashSet<INamedTypeSymbol>.GetInstance();

            var xmlWriter = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlWriter);
            if (xmlWriter != null)
            {
                builder.Add(xmlWriter);
            }

            var webUILiteralControl = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebUILiteralControl);
            if (webUILiteralControl != null)
            {
                builder.Add(webUILiteralControl);
            }

            var unitTestingAssert = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            if (unitTestingAssert != null)
            {
                builder.Add(unitTestingAssert);
            }

            var unitTestingCollectionAssert = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingCollectionAssert);
            if (unitTestingCollectionAssert != null)
            {
                builder.Add(unitTestingCollectionAssert);
            }

            var unitTestingCollectionStringAssert = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingStringAssert);
            if (unitTestingCollectionStringAssert != null)
            {
                builder.Add(unitTestingCollectionStringAssert);
            }

            return builder.ToImmutableAndFree();
        }

        private static bool ShouldBeLocalized(
            IParameterSymbol parameterSymbol,
            IPropertySymbol? containingPropertySymbol,
            INamedTypeSymbol? localizableStateAttributeSymbol,
            INamedTypeSymbol? conditionalAttributeSymbol,
            INamedTypeSymbol? systemConsoleSymbol,
            ImmutableHashSet<INamedTypeSymbol> typesToIgnore,
            bool useNamingHeuristic)
        {
            Debug.Assert(parameterSymbol.ContainingSymbol.Kind == SymbolKind.Method);

            if (parameterSymbol.Type.SpecialType != SpecialType.System_String)
            {
                return false;
            }

            // Verify LocalizableAttributeState
            if (localizableStateAttributeSymbol != null)
            {
                LocalizableAttributeState localizableAttributeState = GetLocalizableAttributeState(parameterSymbol, localizableStateAttributeSymbol);
                switch (localizableAttributeState)
                {
                    case LocalizableAttributeState.False:
                        return false;

                    case LocalizableAttributeState.True:
                        return true;

                    default:
                        break;
                }
            }

            // FxCop compat checks.
            if (typesToIgnore.Contains(parameterSymbol.ContainingType) ||
                parameterSymbol.ContainingSymbol.HasAttribute(conditionalAttributeSymbol))
            {
                return false;
            }

            // FxCop compat: For overrides, check for localizability of the corresponding parameter in the overridden method.
            var method = (IMethodSymbol)parameterSymbol.ContainingSymbol;
            if (method.IsOverride &&
                method.OverriddenMethod?.Parameters.Length == method.Parameters.Length)
            {
                int parameterIndex = method.GetParameterIndex(parameterSymbol);
                IParameterSymbol overridenParameter = method.OverriddenMethod.Parameters[parameterIndex];
                if (Equals(overridenParameter.Type, parameterSymbol.Type))
                {
                    return ShouldBeLocalized(overridenParameter, containingPropertySymbol, localizableStateAttributeSymbol, conditionalAttributeSymbol, systemConsoleSymbol, typesToIgnore, useNamingHeuristic);
                }
            }

            if (useNamingHeuristic)
            {
                if (IsLocalizableByNameHeuristic(parameterSymbol) ||
                    containingPropertySymbol != null && IsLocalizableByNameHeuristic(containingPropertySymbol))
                {
                    return true;
                }
            }

            if (method.ContainingType.Equals(systemConsoleSymbol) &&
                (method.Name.Equals("Write", StringComparison.Ordinal) ||
                 method.Name.Equals("WriteLine", StringComparison.Ordinal)) &&
                (parameterSymbol.Name.Equals("format", StringComparison.OrdinalIgnoreCase) ||
                 parameterSymbol.Name.Equals("value", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;

            // FxCop compat: If a localizable attribute isn't defined then fall back to name heuristics.
            static bool IsLocalizableByNameHeuristic(ISymbol symbol) =>
                symbol.Name.Equals("message", StringComparison.OrdinalIgnoreCase) ||
                symbol.Name.Equals("text", StringComparison.OrdinalIgnoreCase) ||
                symbol.Name.Equals("caption", StringComparison.OrdinalIgnoreCase);
        }

        private static LocalizableAttributeState GetLocalizableAttributeState(ISymbol symbol, INamedTypeSymbol localizableAttributeTypeSymbol)
        {
            if (symbol == null)
            {
                return LocalizableAttributeState.Undefined;
            }

            LocalizableAttributeState localizedState = GetLocalizableAttributeStateCore(symbol.GetAttributes(), localizableAttributeTypeSymbol);
            if (localizedState != LocalizableAttributeState.Undefined)
            {
                return localizedState;
            }

            ISymbol containingSymbol = (symbol as IMethodSymbol)?.AssociatedSymbol is IPropertySymbol propertySymbol ? propertySymbol : symbol.ContainingSymbol;
            return GetLocalizableAttributeState(containingSymbol, localizableAttributeTypeSymbol);
        }

        private static LocalizableAttributeState GetLocalizableAttributeStateCore(ImmutableArray<AttributeData> attributeList, INamedTypeSymbol localizableAttributeTypeSymbol)
        {
            var localizableAttribute = attributeList.FirstOrDefault(attr => localizableAttributeTypeSymbol.Equals(attr.AttributeClass));
            if (localizableAttribute != null &&
                localizableAttribute.AttributeConstructor.Parameters.Length == 1 &&
                localizableAttribute.AttributeConstructor.Parameters[0].Type.SpecialType == SpecialType.System_Boolean &&
                localizableAttribute.ConstructorArguments.Length == 1 &&
                localizableAttribute.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                localizableAttribute.ConstructorArguments[0].Value is bool isLocalizable)
            {
                return isLocalizable ? LocalizableAttributeState.True : LocalizableAttributeState.False;
            }

            return LocalizableAttributeState.Undefined;
        }

        private static string FormatLiteralValues(IEnumerable<string> literalValues)
        {
            var literals = new StringBuilder();
            foreach (string literal in literalValues.Order())
            {
                // sanitize the literal to ensure it's not multi-line
                // replace any newline characters with a space
                var sanitizedLiteral = literal.Replace((char)13, ' ');
                sanitizedLiteral = sanitizedLiteral.Replace((char)10, ' ');

                if (literals.Length > 0)
                {
                    literals.Append(", ");
                }

                literals.Append(sanitizedLiteral);
            }

            return literals.ToString();
        }

        /// <summary>
        /// Returns true if the given string looks like an XML/HTML tag
        /// </summary>
        private static bool LooksLikeXmlTag(string literal)
        {
            // Call the trim function to remove any spaces around the beginning and end of the string so we can more accurately detect
            // XML strings
            string trimmedLiteral = literal.Trim();
            return trimmedLiteral.Length > 2 && trimmedLiteral[0] == '<' && trimmedLiteral[^1] == '>';
        }

        /// <summary>
        /// Returns true if any character in literalValues is not a control character
        /// </summary>
        private static bool LiteralValuesHaveNonControlCharacters(IEnumerable<string?> literalValues)
        {
            foreach (string? literal in literalValues)
            {
                if (literal == null)
                {
                    continue;
                }

                foreach (char ch in literal)
                {
                    if (!char.IsControl(ch))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
