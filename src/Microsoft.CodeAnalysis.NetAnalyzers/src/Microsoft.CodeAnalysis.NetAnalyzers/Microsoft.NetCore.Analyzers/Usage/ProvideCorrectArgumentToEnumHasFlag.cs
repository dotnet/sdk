// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Usage
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2248: <inheritdoc cref="ProvideCorrectArgumentToEnumHasFlagTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ProvideCorrectArgumentToEnumHasFlag : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2248";

        internal static readonly DiagnosticDescriptor DifferentTypeRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ProvideCorrectArgumentToEnumHasFlagTitle)),
            CreateLocalizableResourceString(nameof(ProvideCorrectArgumentToEnumHasFlagMessageDifferentType)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(ProvideCorrectArgumentToEnumHasFlagDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DifferentTypeRule);

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
