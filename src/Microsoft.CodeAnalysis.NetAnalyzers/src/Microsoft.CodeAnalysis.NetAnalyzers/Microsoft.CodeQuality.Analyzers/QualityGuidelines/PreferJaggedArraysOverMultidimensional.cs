// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1814: Prefer jagged arrays over multidimensional
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferJaggedArraysOverMultidimensionalAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1814";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferJaggedArraysOverMultidimensionalTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferJaggedArraysOverMultidimensionalMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageReturn = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferJaggedArraysOverMultidimensionalMessageReturn), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageBody = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferJaggedArraysOverMultidimensionalMessageBody), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.PreferJaggedArraysOverMultidimensionalDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor ReturnRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageReturn,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor BodyRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageBody,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, ReturnRule, BodyRule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            analysisContext.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            analysisContext.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            analysisContext.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ArrayCreation);
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

            // If its an override then don't report it as it can only be fixed in the base type.
            if (!property.IsOverride)
            {
                if (IsMultiDimensionalArray(property.Type))
                {
                    context.ReportDiagnostic(property.CreateDiagnostic(DefaultRule, property.Name));
                }

                AnalyzeParameters(context, property.Parameters);
            }
        }

        private static void AnalyzeMethod(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;

            // If its an override then don't report it as it can only be fixed in the base type.
            // If its a getter\setter then we will report on the property instead so skip analyzing the method.
            if (!method.IsOverride && method.AssociatedSymbol == null)
            {
                if (IsMultiDimensionalArray(method.ReturnType))
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(ReturnRule, method.Name, method.ReturnType));
                }

                AnalyzeParameters(context, method.Parameters);
            }
        }

        private static void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            var arrayCreationExpression = (IArrayCreationOperation)context.Operation;

            if (IsMultiDimensionalArray(arrayCreationExpression.Type))
            {
                context.ReportDiagnostic(arrayCreationExpression.Syntax.CreateDiagnostic(BodyRule, context.ContainingSymbol.Name, arrayCreationExpression.Type));
            }
        }

        private static void AnalyzeParameters(SymbolAnalysisContext context, ImmutableArray<IParameterSymbol> parameters)
        {
            foreach (IParameterSymbol parameter in parameters)
            {
                if (IsMultiDimensionalArray(parameter.Type))
                {
                    context.ReportDiagnostic(parameter.CreateDiagnostic(DefaultRule, parameter.Name));
                }
            }
        }

        /// <summary>
        /// Check if the given type or any of its inner element types is a multi dimensional array
        /// </summary>
        private static bool IsMultiDimensionalArray(ITypeSymbol type)
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

            return false;
        }
    }
}