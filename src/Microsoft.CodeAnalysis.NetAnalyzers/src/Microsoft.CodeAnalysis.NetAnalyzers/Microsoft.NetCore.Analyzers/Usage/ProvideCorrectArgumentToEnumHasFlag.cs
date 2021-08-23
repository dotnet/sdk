// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ProvideCorrectArgumentToEnumHasFlag : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2248";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentToEnumHasFlagTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDifferentType = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentToEnumHasFlagMessageDifferentType), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentToEnumHasFlagDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor DifferentTypeRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessageDifferentType,
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DifferentTypeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var flagsAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFlagsAttribute);

                context.RegisterOperationAction(context =>
                {
                    var invocation = (IInvocationOperation)context.Operation;

                    if (invocation.TargetMethod.ContainingType.SpecialType == SpecialType.System_Enum &&
                        invocation.Arguments.Length == 1 &&
                        invocation.Instance != null &&
                        invocation.TargetMethod.Name == "HasFlag" &&
                        invocation.Arguments[0].Value is IConversionOperation conversion &&
                        invocation.Instance.Type != null &&
                        invocation.Instance.Type.TypeKind != TypeKind.TypeParameter &&
                        conversion.Operand.Type?.TypeKind != TypeKind.TypeParameter &&
                        !invocation.Instance.Type.Equals(conversion.Operand.Type))
                    {
                        context.ReportDiagnostic(invocation.CreateDiagnostic(DifferentTypeRule, GetArgumentTypeName(conversion), invocation.Instance.Type.Name));
                    }
                }, OperationKind.Invocation);
            });
        }

        private static string GetArgumentTypeName(IConversionOperation conversion)
        {
            if (conversion.Operand.Type != null)
            {
                return conversion.Operand.Type.Name;
            }

            return conversion.Language == LanguageNames.VisualBasic
                ? "<Nothing>"
                : "<null>";
        }
    }
}
