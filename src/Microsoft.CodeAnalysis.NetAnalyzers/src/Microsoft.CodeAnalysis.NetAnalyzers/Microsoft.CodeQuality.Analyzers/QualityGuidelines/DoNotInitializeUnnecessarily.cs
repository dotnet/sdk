// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>CA1805: Do not initialize unnecessarily.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotInitializeUnnecessarilyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1805";

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
            new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterOperationAction(context =>
            {
                var init = (IFieldInitializerOperation)context.Operation;
                IFieldSymbol? field = init.InitializedFields.FirstOrDefault();
                if (field != null &&
                    !field.IsConst &&
                    init.Value != null &&
                    field.GetAttributes().Length == 0 && // in case of attributes that impact nullability analysis
                    UsesKnownDefaultValue(init.Value, field.Type))
                {
                    context.ReportDiagnostic(init.CreateDiagnostic(DefaultRule, field.Name));
                }
            }, OperationKind.FieldInitializer);

            analysisContext.RegisterOperationAction(context =>
            {
                var init = (IPropertyInitializerOperation)context.Operation;
                IPropertySymbol? prop = init.InitializedProperties.FirstOrDefault();
                if (prop != null &&
                    init.Value != null &&
                    prop.GetAttributes().Length == 0 && // in case of attributes that impact nullability analysis
                    UsesKnownDefaultValue(init.Value, prop.Type))
                {
                    context.ReportDiagnostic(init.CreateDiagnostic(DefaultRule, prop.Name));
                }
            }, OperationKind.PropertyInitializer);

            static bool UsesKnownDefaultValue(IOperation value, ITypeSymbol type)
            {
                // Skip through built-in conversions
                while (value is IConversionOperation conversion && !conversion.Conversion.IsUserDefined)
                {
                    value = conversion.Operand;
                }

                // If this is default(T) or new ValueType(), it's the default.
                if (value is IDefaultValueOperation ||
                    (type.IsValueType && value is IObjectCreationOperation oco && oco.Arguments.Length == 0))
                {
                    return !IsNullSuppressed(value);
                }

                // Then if this isn't a literal, it's not the default.
                if (!(value is ILiteralOperation literal) || !literal.ConstantValue.HasValue)
                {
                    return false;
                }

                // If this is a reference type or a nullable, it's the default if the value is null.
                if (type.IsReferenceTypeOrNullableValueType())
                {
                    return literal.ConstantValue.Value is null && !IsNullSuppressed(literal);
                }

                // Finally, if this is a primitive (including enums), it's the default if it's 0/false.
                return
                    (type.IsPrimitiveType() || type.TypeKind == TypeKind.Enum) &&
                    literal.ConstantValue.Value switch
                    {
                        bool b => b == false,
                        char c => c == 0,
                        byte b => b == 0,
                        sbyte s => s == 0,
                        ushort u => u == 0,
                        short s => s == 0,
                        uint u => u == 0,
                        int i => i == 0,
                        ulong u => u == 0,
                        long l => l == 0,
                        float f => f == 0f,
                        double d => d == 0,
                        _ => false
                    };

                // Special-case `null!`/`default!` to not warn about it, as it's often used to suppress nullable warnings on fields.
                static bool IsNullSuppressed(IOperation op) =>
                    op.Syntax?.Parent is PostfixUnaryExpressionSyntax parent &&
                    parent.Kind() == CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression;
            }
        }
    }
}