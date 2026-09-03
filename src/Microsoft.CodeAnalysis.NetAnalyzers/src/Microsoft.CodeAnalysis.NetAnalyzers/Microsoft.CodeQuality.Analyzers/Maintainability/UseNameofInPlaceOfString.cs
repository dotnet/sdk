// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1507: <inheritdoc cref="UseNameOfInPlaceOfStringTitle"/>
    /// </summary>
    public abstract class UseNameofInPlaceOfStringAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1507";
        private const string ParamName = "paramName";
        private const string PropertyName = "propertyName";
        internal const string StringText = "StringText";

        internal static readonly DiagnosticDescriptor RuleWithSuggestion = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseNameOfInPlaceOfStringTitle)),
            CreateLocalizableResourceString(nameof(UseNameOfInPlaceOfStringMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(UseNameOfInPlaceOfStringDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(RuleWithSuggestion);

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
            if (argument.Parameter == null
                || argument.Value.Kind != OperationKind.Literal
                || argument.ArgumentKind != ArgumentKind.Explicit
                || argument.Value.Type?.SpecialType != SpecialType.System_String
                || !argument.Value.ConstantValue.HasValue
                || argument.Value.ConstantValue.Value is not string stringText)
            {
                return;
            }

            // argument.Parameter will not be null here. The only case for it to be null is an __arglist argument, for which
            // the argument type will not be of SpecialType.System_String.
            switch (argument.Parameter.Name)
            {
                case ParamName:
                    if (HasAnyParameterMatchInScope(context, stringText))
                    {
                        context.ReportDiagnostic(argument.Value.CreateDiagnostic(RuleWithSuggestion, stringText));
                    }

                    return;
                case PropertyName:
                    if (HasAnyPropertyMatchInScope(context, stringText))
                    {
                        context.ReportDiagnostic(argument.Value.CreateDiagnostic(RuleWithSuggestion, stringText));
                    }

                    return;
                default:
                    return;
            }
        }

        private static bool HasAnyPropertyMatchInScope(OperationAnalysisContext context, string stringText)
        {
            var containingType = context.ContainingSymbol.ContainingType;
            return containingType?.GetMembers(stringText).Any(m => m.Kind == SymbolKind.Property) == true;
        }

        private static bool HasAnyParameterMatchInScope(OperationAnalysisContext context, string stringText)
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
                    _ => null
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
