// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA2027: <inheritdoc cref="AvoidRedundantRegexIsMatchBeforeMatchMessage"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidRedundantRegexIsMatchBeforeMatch : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2027";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidRedundantRegexIsMatchBeforeMatchTitle)),
            CreateLocalizableResourceString(nameof(AvoidRedundantRegexIsMatchBeforeMatchMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(AvoidRedundantRegexIsMatchBeforeMatchDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsRegex, out var regexType))
                {
                    return;
                }

                var regexIsMatchSymbols = regexType.GetMembers("IsMatch").OfType<IMethodSymbol>().ToImmutableArray();
                var regexMatchSymbols = regexType.GetMembers("Match").OfType<IMethodSymbol>().ToImmutableArray();

                if (regexIsMatchSymbols.IsEmpty || regexMatchSymbols.IsEmpty)
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var conditional = (IConditionalOperation)context.Operation;

                    // Check if condition is Regex.IsMatch call (direct or negated)
                    if (!IsRegexIsMatchCall(conditional.Condition, regexIsMatchSymbols, out var isMatchCall, out bool isNegated))
                    {
                        return;
                    }

                    // For normal IsMatch, look in when-true branch for corresponding Match call
                    if (!isNegated)
                    {
                        if (FindMatchCallInBranch(conditional.WhenTrue, regexMatchSymbols, isMatchCall, context.Operation, out var matchCall))
                        {
                            context.ReportDiagnostic(isMatchCall.CreateDiagnostic(Rule));
                            return;
                        }
                    }
                    // For negated IsMatch with early return pattern, check subsequent operations
                    else if (IsEarlyReturnPattern(conditional))
                    {
                        // Look for Match calls after the conditional in the parent block
                        if (FindMatchCallAfterConditional(conditional, regexMatchSymbols, isMatchCall, context.Operation, out var subsequentMatchCall))
                        {
                            context.ReportDiagnostic(isMatchCall.CreateDiagnostic(Rule));
                            return;
                        }
                    }
                }, OperationKind.Conditional);
            });
        }

        private static bool IsRegexIsMatchCall(IOperation condition, ImmutableArray<IMethodSymbol> regexIsMatchSymbols, out IInvocationOperation isMatchCall, out bool isNegated)
        {
            // Handle unwrapping of conversions and parenthesized expressions
            var unwrapped = condition.WalkDownConversion();

            // Check for negation
            isNegated = false;
            if (unwrapped is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unaryOp)
            {
                isNegated = true;
                unwrapped = unaryOp.Operand.WalkDownConversion();
            }

            if (unwrapped is IInvocationOperation invocation &&
                regexIsMatchSymbols.Contains(invocation.TargetMethod, SymbolEqualityComparer.Default))
            {
                isMatchCall = invocation;
                return true;
            }

            isMatchCall = null!;
            return false;
        }

        private static bool FindMatchCallInBranch(IOperation branch, ImmutableArray<IMethodSymbol> regexMatchSymbols, IInvocationOperation isMatchCall, IOperation conditionalOperation, out IInvocationOperation matchCall)
        {
            // Search for the first Match call that matches criteria
            return FindMatchCallRecursive(branch, regexMatchSymbols, isMatchCall, conditionalOperation, out matchCall);
        }

        private static bool FindMatchCallRecursive(IOperation operation, ImmutableArray<IMethodSymbol> regexMatchSymbols, IInvocationOperation isMatchCall, IOperation conditionalOperation, out IInvocationOperation matchCall)
        {
            if (operation is IInvocationOperation invocation &&
                regexMatchSymbols.Contains(invocation.TargetMethod, SymbolEqualityComparer.Default))
            {
                if (AreInvocationArgumentsEqual(isMatchCall, invocation, conditionalOperation) &&
                    AreInvocationsOnSameInstance(isMatchCall, invocation, conditionalOperation))
                {
                    matchCall = invocation;
                    return true;
                }
            }

            foreach (var child in operation.Children)
            {
                if (FindMatchCallRecursive(child, regexMatchSymbols, isMatchCall, conditionalOperation, out matchCall))
                {
                    return true;
                }
            }

            matchCall = null!;
            return false;
        }

        private static bool IsEarlyReturnPattern(IConditionalOperation conditional)
        {
            // Check if the when-true branch is an early return or throw (not break/continue/goto)
            if (conditional.WhenTrue is IBlockOperation block)
            {
                foreach (var statement in block.Operations)
                {
                    if (statement is IReturnOperation or IThrowOperation)
                    {
                        return true;
                    }
                }
            }
            else if (conditional.WhenTrue is IReturnOperation or IThrowOperation)
            {
                return true;
            }

            return false;
        }

        private static bool FindMatchCallAfterConditional(IConditionalOperation conditional, ImmutableArray<IMethodSymbol> regexMatchSymbols, IInvocationOperation isMatchCall, IOperation conditionalOperation, out IInvocationOperation matchCall)
        {
            // Navigate to the parent block to find subsequent operations
            var parent = conditional.Parent;
            while (parent is not null && parent is not IBlockOperation)
            {
                parent = parent.Parent;
            }

            if (parent is IBlockOperation parentBlock)
            {
                bool foundConditional = false;
                foreach (var operation in parentBlock.Operations)
                {
                    if (operation == conditional)
                    {
                        foundConditional = true;
                        continue;
                    }

                    if (foundConditional)
                    {
                        // Look for the first Match call that matches criteria
                        if (FindMatchCallRecursive(operation, regexMatchSymbols, isMatchCall, conditionalOperation, out matchCall))
                        {
                            return true;
                        }
                    }
                }
            }

            matchCall = null!;
            return false;
        }

        private static bool AreInvocationsOnSameInstance(IInvocationOperation invocation1, IInvocationOperation invocation2, IOperation conditionalOperation)
        {
            var instance1 = invocation1.Instance?.WalkDownConversion();
            var instance2 = invocation2.Instance?.WalkDownConversion();

            // Both are static calls
            if (instance1 is null && instance2 is null)
            {
                return true;
            }

            // One is static, other is not
            if (instance1 is null || instance2 is null)
            {
                return false;
            }

            // Check if instances are the same
            bool sameInstance = (instance1, instance2) switch
            {
                (ILocalReferenceOperation localRef1, ILocalReferenceOperation localRef2) => SymbolEqualityComparer.Default.Equals(localRef1.Local, localRef2.Local),
                (IParameterReferenceOperation paramRef1, IParameterReferenceOperation paramRef2) => SymbolEqualityComparer.Default.Equals(paramRef1.Parameter, paramRef2.Parameter),
                (IFieldReferenceOperation fieldRef1, IFieldReferenceOperation fieldRef2) when 
                    fieldRef1.Member is IFieldSymbol field1 && fieldRef2.Member is IFieldSymbol field2 && field1.IsReadOnly && field2.IsReadOnly => 
                    SymbolEqualityComparer.Default.Equals(fieldRef1.Member, fieldRef2.Member),
                _ => false,
            };

            if (!sameInstance)
            {
                return false;
            }

            // For locals and parameters, check if they're modified between calls
            if (instance1 is ILocalReferenceOperation localRef)
            {
                if (conditionalOperation is IConditionalOperation conditional)
                {
                    return !HasAssignmentToSymbol(localRef.Local, conditional.WhenTrue);
                }
            }
            else if (instance1 is IParameterReferenceOperation paramRef)
            {
                if (conditionalOperation is IConditionalOperation conditional)
                {
                    return !HasAssignmentToSymbol(paramRef.Parameter, conditional.WhenTrue);
                }
            }

            return true;
        }

        private static bool AreInvocationArgumentsEqual(IInvocationOperation invocation1, IInvocationOperation invocation2, IOperation conditionalOperation)
        {
            var args1 = invocation1.Arguments;
            var args2 = invocation2.Arguments;

            if (args1.Length != args2.Length)
            {
                return false;
            }

            for (int i = 0; i < args1.Length; i++)
            {
                if (!AreArgumentValuesEqual(args1[i].Value, args2[i].Value))
                {
                    return false;
                }

                // Check if the argument could have been modified between the two calls
                if (CouldArgumentBeModifiedBetween(args1[i].Value, invocation1, invocation2, conditionalOperation))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CouldArgumentBeModifiedBetween(IOperation? argument, IOperation start, IOperation end, IOperation conditionalOperation)
        {
            if (argument is null)
            {
                return false;
            }

            argument = argument.WalkDownConversion();

            // Constants can't be modified
            if (argument.ConstantValue.HasValue)
            {
                return false;
            }

            // Parameters and fields could potentially be modified, but we'll be conservative
            // and only allow local variables and parameters that we can verify aren't assigned
            if (argument is ILocalReferenceOperation localRef)
            {
                // Check for assignments to this local between the calls
                // We need to search the branch between IsMatch and Match
                if (conditionalOperation is IConditionalOperation conditional)
                {
                    return HasAssignmentToSymbol(localRef.Local, conditional.WhenTrue);
                }
                return false;
            }

            if (argument is IParameterReferenceOperation paramRef)
            {
                // Check if the parameter is assigned between calls
                if (conditionalOperation is IConditionalOperation conditional)
                {
                    return HasAssignmentToSymbol(paramRef.Parameter, conditional.WhenTrue);
                }
                return false;
            }

            // For properties and fields, be conservative and assume they could change
            if (argument is IPropertyReferenceOperation or IFieldReferenceOperation)
            {
                return true;
            }

            // Other cases - be conservative
            return true;
        }

        private static bool HasAssignmentToSymbol(ISymbol symbol, IOperation operation)
        {
            // Check if this operation is an assignment to the symbol
            if (operation is ISimpleAssignmentOperation assignment)
            {
                if (assignment.Target is ILocalReferenceOperation targetLocal &&
                    SymbolEqualityComparer.Default.Equals(targetLocal.Local, symbol))
                {
                    return true;
                }
                if (assignment.Target is IParameterReferenceOperation targetParam &&
                    SymbolEqualityComparer.Default.Equals(targetParam.Parameter, symbol))
                {
                    return true;
                }
            }

            // Check for compound assignments
            if (operation is ICompoundAssignmentOperation compoundAssignment)
            {
                if (compoundAssignment.Target is ILocalReferenceOperation compoundTargetLocal &&
                    SymbolEqualityComparer.Default.Equals(compoundTargetLocal.Local, symbol))
                {
                    return true;
                }
                if (compoundAssignment.Target is IParameterReferenceOperation compoundTargetParam &&
                    SymbolEqualityComparer.Default.Equals(compoundTargetParam.Parameter, symbol))
                {
                    return true;
                }
            }

            // Check for increments/decrements
            if (operation is IIncrementOrDecrementOperation increment)
            {
                if (increment.Target is ILocalReferenceOperation incrementTargetLocal &&
                    SymbolEqualityComparer.Default.Equals(incrementTargetLocal.Local, symbol))
                {
                    return true;
                }
                if (increment.Target is IParameterReferenceOperation incrementTargetParam &&
                    SymbolEqualityComparer.Default.Equals(incrementTargetParam.Parameter, symbol))
                {
                    return true;
                }
            }

            // Recursively check children
            foreach (var child in operation.Children)
            {
                if (HasAssignmentToSymbol(symbol, child))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreArgumentValuesEqual(IOperation? arg1, IOperation? arg2)
        {
            if (arg1 is null || arg2 is null)
            {
                return arg1 is null && arg2 is null;
            }

            arg1 = arg1.WalkDownConversion();
            arg2 = arg2.WalkDownConversion();

            if (arg1.Kind != arg2.Kind)
            {
                return false;
            }

            // Check constant values
            if (arg1.ConstantValue.HasValue && arg2.ConstantValue.HasValue)
            {
                return Equals(arg1.ConstantValue.Value, arg2.ConstantValue.Value);
            }

            if (arg1.ConstantValue.HasValue != arg2.ConstantValue.HasValue)
            {
                return false;
            }

            // Check references
            return arg1 switch
            {
                ILocalReferenceOperation localRef1 when arg2 is ILocalReferenceOperation localRef2 =>
                    SymbolEqualityComparer.Default.Equals(localRef1.Local, localRef2.Local),
                IParameterReferenceOperation paramRef1 when arg2 is IParameterReferenceOperation paramRef2 =>
                    SymbolEqualityComparer.Default.Equals(paramRef1.Parameter, paramRef2.Parameter),
                IFieldReferenceOperation fieldRef1 when arg2 is IFieldReferenceOperation fieldRef2 =>
                    SymbolEqualityComparer.Default.Equals(fieldRef1.Member, fieldRef2.Member),
                IPropertyReferenceOperation propRef1 when arg2 is IPropertyReferenceOperation propRef2 =>
                    SymbolEqualityComparer.Default.Equals(propRef1.Member, propRef2.Member),
                _ => false,
            };
        }
    }
}
