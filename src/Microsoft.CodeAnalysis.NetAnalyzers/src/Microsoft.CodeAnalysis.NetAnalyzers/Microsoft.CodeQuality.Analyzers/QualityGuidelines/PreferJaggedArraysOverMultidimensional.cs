// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1814: <inheritdoc cref="PreferJaggedArraysOverMultidimensionalTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferJaggedArraysOverMultidimensionalAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1814";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(PreferJaggedArraysOverMultidimensionalTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(PreferJaggedArraysOverMultidimensionalDescription));

        internal static readonly DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PreferJaggedArraysOverMultidimensionalMessageDefault)),
            DiagnosticCategory.Performance,
            RuleLevel.CandidateForRemoval,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ReturnRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PreferJaggedArraysOverMultidimensionalMessageReturn)),
            DiagnosticCategory.Performance,
            RuleLevel.CandidateForRemoval,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor BodyRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PreferJaggedArraysOverMultidimensionalMessageBody)),
            DiagnosticCategory.Performance,
            RuleLevel.CandidateForRemoval,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DefaultRule, ReturnRule, BodyRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ArrayCreation);
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            var field = (IFieldSymbol)context.Symbol;

            if (IsMultiDimensionalArray(field.Type))
            {
                context.ReportDiagnostic(field.CreateDiagnostic(DefaultRule, field.Name));
            }
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var property = (IPropertySymbol)context.Symbol;

            // Bail-out when override as it can only be fixed in the base type.
            if (property.IsOverride)
            {
                return;
            }

            if (IsMultiDimensionalArray(property.Type) &&
                !property.IsImplementationOfAnyInterfaceMember())
            {
                context.ReportDiagnostic(property.CreateDiagnostic(DefaultRule, property.Name));
            }

            AnalyzeParameters(context, property, property.Parameters);
        }

        private static void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;

            // Bail-out when override as it can only be fixed in the base type.
            // If its a getter\setter then we will report on the property instead so skip analyzing the method.
            if (method.IsOverride || method.AssociatedSymbol != null)
            {
                return;
            }

            if (IsMultiDimensionalArray(method.ReturnType) &&
                !method.IsImplementationOfAnyInterfaceMember())
            {
                context.ReportDiagnostic(method.CreateDiagnostic(ReturnRule, method.Name, method.ReturnType));
            }

            AnalyzeParameters(context, method, method.Parameters);
        }

        private static void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            var arrayCreationExpression = (IArrayCreationOperation)context.Operation;

            if (IsMultiDimensionalArray(arrayCreationExpression.Type))
            {
                context.ReportDiagnostic(arrayCreationExpression.Syntax.CreateDiagnostic(BodyRule, context.ContainingSymbol.Name, arrayCreationExpression.Type));
            }
        }

        private static void AnalyzeParameters(SymbolAnalysisContext context, ISymbol containingSymbol, ImmutableArray<IParameterSymbol> parameters)
        {
            foreach (IParameterSymbol parameter in parameters)
            {
                if (IsMultiDimensionalArray(parameter.Type) &&
                    !containingSymbol.IsImplementationOfAnyInterfaceMember())
                {
                    context.ReportDiagnostic(parameter.CreateDiagnostic(DefaultRule, parameter.Name));
                }
            }
        }

        /// <summary>
        /// Check if the given type or any of its inner element types is a multi dimensional array
        /// </summary>
        private static bool IsMultiDimensionalArray([NotNullWhen(true)] ITypeSymbol? type)
        {
            if (type != null)
            {
                while (type.TypeKind == TypeKind.Array)
                {
                    var arrayType = (IArrayTypeSymbol)type;

                    if (arrayType.Rank > 1)
                    {
                        return true;
                    }

                    type = arrayType.ElementType;
                }
            }

            return false;
        }
    }
}