﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1820: <inheritdoc cref="TestForEmptyStringsUsingStringLengthTitle"/>
    /// <para>
    /// Comparing strings using the <see cref="string.Length"/> property or the <see cref="string.IsNullOrEmpty"/> method is significantly faster than using <see cref="string.Equals(string)"/>.
    /// This is because Equals executes significantly more MSIL instructions than either IsNullOrEmpty or the number of instructions executed to retrieve the Length property value and compare it to zero.
    /// </para>
    /// <remarks>NOTE: This rule is not supported for VisualBasic. See https://github.com/dotnet/roslyn-analyzers/issues/2684 for details.</remarks>
    /// </summary>
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
    public sealed class TestForEmptyStringsUsingStringLengthAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1820";
        private const string StringEmptyFieldName = "Empty";

        private static readonly DiagnosticDescriptor s_rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(TestForEmptyStringsUsingStringLengthTitle)),
            CreateLocalizableResourceString(nameof(TestForEmptyStringsUsingStringLengthMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,    // Benefits provided might not outweight noise in test code.
            description: CreateLocalizableResourceString(nameof(TestForEmptyStringsUsingStringLengthDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(s_rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var linqExpressionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);

                context.RegisterOperationAction(
                    operationAnalysisContext => AnalyzeInvocationExpression(
                        (IInvocationOperation)operationAnalysisContext.Operation, linqExpressionType,
                        static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                        operationAnalysisContext),
                    OperationKind.Invocation);

                context.RegisterOperationAction(
                    operationAnalysisContext => AnalyzeBinaryExpression(
                        (IBinaryOperation)operationAnalysisContext.Operation, linqExpressionType,
                        static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                        operationAnalysisContext),
                    OperationKind.BinaryOperator);
            });
        }

        /// <summary>
        /// Check to see if we have an invocation to string.Equals that has an empty string as an argument.
        /// </summary>
        private static void AnalyzeInvocationExpression<TContext>(IInvocationOperation invocationOperation,
            INamedTypeSymbol? linqExpressionTreeType, Action<TContext, Diagnostic> reportDiagnostic, TContext context)
        {
            if (!invocationOperation.Arguments.IsEmpty)
            {
                IMethodSymbol methodSymbol = invocationOperation.TargetMethod;
                if (methodSymbol == null
                    || !IsStringEqualsMethod(methodSymbol)
                    || !HasAnEmptyStringArgument(invocationOperation))
                {
                    return;
                }

                // Check if we are in a Expression<Func<T...>> context, in which case it is possible
                // that the underlying call doesn't have the helper so we want to bail-out.
                if (!invocationOperation.IsWithinExpressionTree(linqExpressionTreeType))
                {
                    reportDiagnostic(context, invocationOperation.Syntax.CreateDiagnostic(s_rule));
                }
            }
        }

        /// <summary>
        /// Check to see if we have a equals or not equals expression where an empty string is being
        /// compared.
        /// </summary>
        private static void AnalyzeBinaryExpression<TContext>(IBinaryOperation binaryOperation,
            INamedTypeSymbol? linqExpressionTreeType, Action<TContext, Diagnostic> reportDiagnostic, TContext context)
        {
            if (binaryOperation.OperatorKind is not BinaryOperatorKind.Equals and
                not BinaryOperatorKind.NotEquals)
            {
                return;
            }

            if (binaryOperation.LeftOperand.Type?.SpecialType != SpecialType.System_String ||
                binaryOperation.RightOperand.Type?.SpecialType != SpecialType.System_String)
            {
                return;
            }

            if (!IsEmptyString(binaryOperation.LeftOperand)
                && !IsEmptyString(binaryOperation.RightOperand))
            {
                return;
            }

            // Check if we are in a Expression<Func<T...>> context, in which case it is possible
            // that the underlying call doesn't have the helper so we want to bail-out.
            if (!binaryOperation.IsWithinExpressionTree(linqExpressionTreeType))
            {
                reportDiagnostic(context, binaryOperation.Syntax.CreateDiagnostic(s_rule));
            }
        }

        /// <summary>
        /// Checks if the given method is the string.Equals method.
        /// </summary>
        private static bool IsStringEqualsMethod(IMethodSymbol methodSymbol)
        {
            return string.Equals(methodSymbol.Name, WellKnownMemberNames.ObjectEquals, StringComparison.Ordinal) &&
                   methodSymbol.ContainingType.SpecialType == SpecialType.System_String;
        }

        /// <summary>
        /// Checks if the given expression something that evaluates to a constant string
        /// or the string.Empty field
        /// </summary>
        private static bool IsEmptyString(IOperation expression)
        {
            if (expression == null)
            {
                return false;
            }

            Optional<object?> constantValueOpt = expression.ConstantValue;
            if (constantValueOpt.HasValue)
            {
                return constantValueOpt.Value is string { Length: 0 };
            }

            if (expression.Kind == OperationKind.FieldReference)
            {
                IFieldSymbol field = ((IFieldReferenceOperation)expression).Field;
                return string.Equals(field.Name, StringEmptyFieldName, StringComparison.Ordinal) &&
                    field.Type.SpecialType == SpecialType.System_String;
            }

            return false;
        }

        /// <summary>
        /// Checks if the given invocation has an argument that is an empty string.
        /// </summary>
        private static bool HasAnEmptyStringArgument(IInvocationOperation invocation)
        {
            return invocation.Arguments.Any(arg => IsEmptyString(arg.Value));
        }
    }
}
