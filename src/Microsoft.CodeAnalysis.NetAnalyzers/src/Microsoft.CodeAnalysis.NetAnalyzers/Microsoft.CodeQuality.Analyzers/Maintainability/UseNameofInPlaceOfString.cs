// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
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

            var stringText = (string)argument.Value.ConstantValue.Value;

            // argument.Parameter will not be null here. The only case for it to be null is an __arglist argument, for which
            // the argument type will not be of SpecialType.System_String.
            switch (argument.Parameter.Name)
            {
                case ParamName:
                    var parametersInScope = GetParametersInScope(context);
                    if (HasAMatchInScope(stringText, parametersInScope))
                    {
                        context.ReportDiagnostic(argument.Value.CreateDiagnostic(RuleWithSuggestion, stringText));
                    }
                    return;
                case PropertyName:
                    var propertiesInScope = GetPropertiesInScope(context);
                    if (HasAMatchInScope(stringText, propertiesInScope))
                    {
                        context.ReportDiagnostic(argument.Value.CreateDiagnostic(RuleWithSuggestion, stringText));
                    }
                    return;
                default:
                    return;
            }
        }

        private static IEnumerable<string> GetPropertiesInScope(OperationAnalysisContext context)
        {
            var containingType = context.ContainingSymbol.ContainingType;
            // look for all of the properties in the containing type and return the property names
            if (containingType != null)
            {
                foreach (var property in containingType.GetMembers().OfType<IPropertySymbol>())
                {
                    yield return property.Name;
                }
            }
        }

        internal static IEnumerable<string> GetParametersInScope(OperationAnalysisContext context)
        {
            // get the parameters for the containing method
            foreach (var parameter in context.ContainingSymbol.GetParameters())
            {
                yield return parameter.Name;
            }

            // and loop through the ancestors to find parameters of anonymous functions and local functions
            var parentOperation = context.Operation.Parent;
            while (parentOperation != null)
            {
                if (parentOperation.Kind == OperationKind.AnonymousFunction)
                {
                    var lambdaSymbol = ((IAnonymousFunctionOperation)parentOperation).Symbol;
                    if (lambdaSymbol != null)
                    {
                        foreach (var lambdaParameter in lambdaSymbol.Parameters)
                        {
                            yield return lambdaParameter.Name;
                        }
                    }
                }
                else if (parentOperation.Kind == OperationKind.LocalFunction)
                {
                    var localFunction = ((ILocalFunctionOperation)parentOperation).Symbol;
                    foreach (var localFunctionParameter in localFunction.Parameters)
                    {
                        yield return localFunctionParameter.Name;
                    }
                }

                parentOperation = parentOperation.Parent;
            }
        }

        private static bool HasAMatchInScope(string stringText, IEnumerable<string> searchCollection)
        {
            foreach (var name in searchCollection)
            {
                if (stringText == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}