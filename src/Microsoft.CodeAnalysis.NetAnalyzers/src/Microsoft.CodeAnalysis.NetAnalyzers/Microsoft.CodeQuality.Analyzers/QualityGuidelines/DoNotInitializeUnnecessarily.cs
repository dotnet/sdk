// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>CA1805: Do not initialize unnecessarily.</summary>
    public abstract class DoNotInitializeUnnecessarilyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1805";

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
            new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeHidden_BulkConfigurable,
            new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotInitializeUnnecessarilyDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule);

        /// <summary>
        /// Special-case `null!`/`default!` to not warn about it, as it's often used to suppress nullable warnings on fields.
        /// </summary>
        protected abstract bool IsNullSuppressed(IOperation op);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(context =>
            {
                var init = (IFieldInitializerOperation)context.Operation;
                IFieldSymbol? field = init.InitializedFields.FirstOrDefault();
                if (field != null &&
                    !field.IsConst &&
                    init.Value != null &&
                    field.GetAttributes().IsEmpty && // in case of attributes that impact nullability analysis
                    UsesKnownDefaultValue(init.Value, field.Type))
                {
                    context.ReportDiagnostic(init.CreateDiagnostic(DefaultRule, field.Name));
                }
            }, OperationKind.FieldInitializer);

            context.RegisterOperationAction(context =>
            {
                var init = (IPropertyInitializerOperation)context.Operation;
                IPropertySymbol? prop = init.InitializedProperties.FirstOrDefault();
                if (prop != null &&
                    init.Value != null &&
                    prop.GetAttributes().IsEmpty && // in case of attributes that impact nullability analysis
                    UsesKnownDefaultValue(init.Value, prop.Type))
                {
                    context.ReportDiagnostic(init.CreateDiagnostic(DefaultRule, prop.Name));
                }
            }, OperationKind.PropertyInitializer);
        }

        private bool UsesKnownDefaultValue(IOperation value, ITypeSymbol type)
        {
            // Skip through built-in conversions
            while (value is IConversionOperation conversion && !conversion.Conversion.IsUserDefined)
            {
                value = conversion.Operand;
            }

            // If this might box or assign a value to a nullable, don't warn, as we don't want to warn for
            // something like `object o = default(int);` or `int? i = 0;`.
            if (value.Type != null && value.Type.IsReferenceTypeOrNullableValueType() != type.IsReferenceTypeOrNullableValueType())
            {
                return false;
            }

            // If this is default(T) or new ValueType(), it's the default.
            if (value is IDefaultValueOperation ||
                (type.IsValueType && value is IObjectCreationOperation oco && oco.Arguments.IsEmpty && oco.Initializer is null))
            {
                return !IsNullSuppressed(value);
            }

            // Then if this isn't a literal, it's not the default.
            if (value is not ILiteralOperation literal || !literal.ConstantValue.HasValue)
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
        }
    }
}