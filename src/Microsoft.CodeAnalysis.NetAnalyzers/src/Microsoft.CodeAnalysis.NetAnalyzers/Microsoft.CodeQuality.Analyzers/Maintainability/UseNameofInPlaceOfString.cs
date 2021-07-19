// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1507 Use nameof to express symbol names
    /// </summary>
    public abstract class UseNameofInPlaceOfStringAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1507";
        private const string ParamName = "paramName";
        private const string PropertyName = "propertyName";
        internal const string StringText = "StringText";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseNameOfInPlaceOfStringTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseNameOfInPlaceOfStringMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseNameOfInPlaceOfStringDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor RuleWithSuggestion = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Maintainability,
                                                                         RuleLevel.IdeSuggestion,
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: false,
                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleWithSuggestion);

        protected abstract bool IsApplicableToLanguageVersion(ParseOptions options);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(AnalyzeArgument, OperationKind.Argument);
        }

        private void AnalyzeArgument(OperationAnalysisContext context)
        {
            if (!IsApplicableToLanguageVersion(context.Operation.Syntax.SyntaxTree.Options))
            {
                return;
            }

            var argument = (IArgumentOperation)context.Operation;
            if (argument.Value.Kind != OperationKind.Literal
                || argument.ArgumentKind != ArgumentKind.Explicit
                || argument.Value.Type.SpecialType != SpecialType.System_String)
            {
                return;
            }

            if (argument.Parameter == null)
            {
                return;
            }

            var stringText = (string)argument.Value.ConstantValue.Value;

            var matchingParameter = argument.Parameter;

            switch (matchingParameter.Name)
            {
                case ParamName:
                    if (HasAParameterMatchInScope(context, stringText))
                    {
                        context.ReportDiagnostic(argument.Value.CreateDiagnostic(RuleWithSuggestion, stringText));
                    }
                    return;
                case PropertyName:
                    if (HasAPropertyMatchInScope(context, stringText))
                    {
                        context.ReportDiagnostic(argument.Value.CreateDiagnostic(RuleWithSuggestion, stringText));
                    }
                    return;
                default:
                    return;
            }
        }

        private static bool HasAPropertyMatchInScope(OperationAnalysisContext context, string stringText)
        {
            var containingType = context.ContainingSymbol.ContainingType;
            return containingType?.GetMembers(stringText).Any(m => m.Kind == SymbolKind.Property) == true;
        }

        private static bool HasAParameterMatchInScope(OperationAnalysisContext context, string stringText)
        {
            // get the parameters for the containing method
            foreach (var parameter in context.ContainingSymbol.GetParameters())
            {
                if (parameter.Name == stringText)
                {
                    return true;
                }
            }

            // and loop through the ancestors to find parameters of anonymous functions and local functions
            var parentOperation = context.Operation.Parent;
            while (parentOperation != null)
            {
                IMethodSymbol? methodSymbol = parentOperation switch
                {
                    IAnonymousFunctionOperation anonymousOperation => anonymousOperation.Symbol,
                    ILocalFunctionOperation localFunctionOperation => localFunctionOperation.Symbol,
                    _ => null;
                };

                if (methodSymbol is not null)
                {
                    foreach (var methodParameter in methodSymbol.Parameters)
                    {
                        if (methodParameter.Name == stringText)
                        {
                            return true;
                        }
                    }
                }

                parentOperation = parentOperation.Parent;
            }

            return false;
        }
    }
}
