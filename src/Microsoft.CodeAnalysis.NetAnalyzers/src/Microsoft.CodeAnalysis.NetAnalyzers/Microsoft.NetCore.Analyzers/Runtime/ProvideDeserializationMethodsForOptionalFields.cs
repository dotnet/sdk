// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2239: Provide deserialization methods for optional fields
    /// </summary>
    public abstract class ProvideDeserializationMethodsForOptionalFieldsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2239";

        /*private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(ProvideDeserializationMethodsForOptionalFieldsTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(ProvideDeserializationMethodsForOptionalFieldsDescription));

        internal static readonly DiagnosticDescriptor OnDeserializedRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ProvideDeserializationMethodsForOptionalFieldsMessageOnDeserialized)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor OnDeserializingRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ProvideDeserializationMethodsForOptionalFieldsMessageOnDeserializing)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
        //ImmutableArray.Create(OnDeserializedRule, OnDeserializingRule);

#pragma warning disable RS1025 // Configure generated code analysis
        public override void Initialize(AnalysisContext context)
#pragma warning restore RS1025 // Configure generated code analysis
        {
            context.EnableConcurrentExecution();

            // TODO: Configure generated code analysis.
            //analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        }
    }
}