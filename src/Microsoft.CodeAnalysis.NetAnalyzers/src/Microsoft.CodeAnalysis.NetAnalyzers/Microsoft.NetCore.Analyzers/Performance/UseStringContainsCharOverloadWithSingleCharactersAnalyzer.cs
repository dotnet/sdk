﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1847: <inheritdoc cref="UseStringContainsCharOverloadWithSingleCharactersTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseStringContainsCharOverloadWithSingleCharactersAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1847 = nameof(CA1847);

        internal static readonly DiagnosticDescriptor s_rule_CA1847 = DiagnosticDescriptorHelper.Create(
            CA1847,
            CreateLocalizableResourceString(nameof(UseStringContainsCharOverloadWithSingleCharactersTitle)),
            CreateLocalizableResourceString(nameof(UseStringContainsCharOverloadWithSingleCharactersMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(UseStringContainsCharOverloadWithSingleCharactersDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(s_rule_CA1847);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CheckIfRuleIsApplicableAndRegister);
        }

        private void CheckIfRuleIsApplicableAndRegister(CompilationStartAnalysisContext context)
        {
            var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
            var charType = context.Compilation.GetSpecialType(SpecialType.System_Char);

            // Bail out of further evaluations in special cases where string/char types arent even available
            if (stringType is null || charType is null)
                return;

            var ruleIsApplicable = stringType.GetMembers("Contains").OfType<IMethodSymbol>().Any(m =>
            {
                if (m.Parameters.Length > 0)
                {
                    return m.Parameters[0].Type.SpecialType == SpecialType.System_Char;
                }

                return false;
            });

            if (ruleIsApplicable)
                context.RegisterOperationAction(AnalyseContainsUsage, OperationKind.Invocation);
        }

        private void AnalyseContainsUsage(OperationAnalysisContext context)
        {
            var invocationOperation = (IInvocationOperation)context.Operation;
            if (ShouldAnalyzeInvocation(invocationOperation))
            {
                var argumentOperation = invocationOperation.Arguments.GetArgumentForParameterAtIndex(0);
                if (CheckForViolation(argumentOperation))
                    context.ReportDiagnostic(argumentOperation.CreateDiagnostic(s_rule_CA1847));
            }

            static bool ShouldAnalyzeInvocation(IInvocationOperation invocationOperation)
            {
                return invocationOperation.TargetMethod is IMethodSymbol invokedMethod
                                && invokedMethod.ContainingType.SpecialType.Equals(SpecialType.System_String)
                                && invokedMethod.Name == "Contains"
                                && invokedMethod.Parameters.Length > 0
                                && invokedMethod.Parameters[0].Type.SpecialType.Equals(SpecialType.System_String);
            }

            static bool CheckForViolation(IArgumentOperation primaryArgument)
            {
                if (primaryArgument.Value is ILiteralOperation literalOperation
                    && literalOperation.ConstantValue.HasValue
                    && literalOperation.ConstantValue.Value is string constantString
                    && constantString.Length == 1)
                {
                    return true;
                }

                return false;
            }
        }
    }
}
