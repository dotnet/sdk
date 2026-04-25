// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
    /// CA2028: Avoid redundant Regex.IsMatch before Regex.Match
    /// <para>
    /// Detects the pattern where <c>Regex.IsMatch</c> is used as a condition and then
    /// <c>Regex.Match</c> is called inside the body with the same arguments, causing
    /// the regex engine to execute twice. The fix is to call <c>Regex.Match</c> once
    /// and check <c>Success</c>.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidRedundantRegexIsMatchBeforeMatch : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2028";

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

            context.RegisterCompilationStartAction(compilationContext =>
            {
                if (!compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextRegularExpressionsRegex, out var regexType))
                {
                    return;
                }

                var isMatchMembers = regexType.GetMembers("IsMatch");
                var matchMembers = regexType.GetMembers("Match");

                if (isMatchMembers.IsEmpty || matchMembers.IsEmpty)
                {
                    return;
                }

                compilationContext.RegisterOperationAction(operationContext =>
                {
                    var conditional = (IConditionalOperation)operationContext.Operation;

                    // The condition must be a direct call to Regex.IsMatch (not negated).
                    if (GetUnwrappedInvocation(conditional.Condition) is not IInvocationOperation isMatchInvocation ||
                        isMatchInvocation.TargetMethod.Name != "IsMatch" ||
                        !isMatchMembers.Contains(isMatchInvocation.TargetMethod, SymbolEqualityComparer.Default))
                    {
                        return;
                    }

                    if (conditional.WhenTrue is null)
                    {
                        return;
                    }

                    // Search direct children of the WhenTrue block for Regex.Match calls.
                    IInvocationOperation? matchInvocation = FindMatchingMatchCall(
                        conditional.WhenTrue, isMatchInvocation, matchMembers);

                    if (matchInvocation is null)
                    {
                        return;
                    }

                    // Report diagnostic on the IsMatch call, with additional location on the Match call.
                    operationContext.ReportDiagnostic(
                        isMatchInvocation.CreateDiagnostic(
                            Rule,
                            ImmutableArray.Create(matchInvocation.Syntax.GetLocation()),
                            properties: null));
                }, OperationKind.Conditional);
            });
        }

        /// <summary>
        /// Unwraps parenthesized and conversion operations to get the underlying invocation.
        /// </summary>
        private static IInvocationOperation? GetUnwrappedInvocation(IOperation? operation)
        {
            if (operation is null)
            {
                return null;
            }

            // Walk through parentheses and conversions (e.g., implicit bool conversion)
            operation = operation.WalkDownParentheses().WalkDownConversion();

            return operation as IInvocationOperation;
        }

        /// <summary>
        /// Searches the WhenTrue block for a Regex.Match call whose arguments are semantically
        /// equivalent to the given IsMatch call.
        /// </summary>
        private static IInvocationOperation? FindMatchingMatchCall(
            IOperation whenTrue,
            IInvocationOperation isMatchInvocation,
            ImmutableArray<ISymbol> matchMembers)
        {
            // If WhenTrue is not a block, check the single operation directly
            // to avoid allocating a one-element ImmutableArray.
            if (whenTrue is not IBlockOperation block)
            {
                return TryFindMatchInOperation(whenTrue, isMatchInvocation, matchMembers);
            }

            // Collect local/parameter symbols referenced by the IsMatch arguments.
            // If any of these are written to before a candidate Match call, the
            // values may differ and the calls are not truly redundant.
            HashSet<ISymbol>? trackedSymbols = null;
            CollectReferencedMutableSymbols(isMatchInvocation, ref trackedSymbols);

            foreach (var op in block.Operations)
            {
                // If this statement writes to any tracked symbol, stop searching.
                // Any Match call in this or later statements could see a different value.
                if (trackedSymbols is not null && ContainsWriteToSymbols(op, trackedSymbols))
                {
                    return null;
                }

                // Look for Match calls in direct children only — don't recurse into
                // lambdas, local functions, or nested blocks (except expression-level nesting).
                if (TryFindMatchInOperation(op, isMatchInvocation, matchMembers) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to find a matching Regex.Match invocation within a single statement operation.
        /// Only looks at expression-level nesting (e.g., within variable declarations, assignments,
        /// member access chains) — does NOT recurse into nested control flow, lambdas, or local functions.
        /// </summary>
        private static IInvocationOperation? TryFindMatchInOperation(
            IOperation operation,
            IInvocationOperation isMatchInvocation,
            ImmutableArray<ISymbol> matchMembers)
        {
            // Handle variable declaration: Match m = Regex.Match(...)
            if (operation is IVariableDeclarationGroupOperation declGroup)
            {
                foreach (var declaration in declGroup.Declarations)
                {
                    // In VB, the initializer is on the declaration (not the declarator).
                    if (declaration.Initializer?.Value is { } declInitValue)
                    {
                        var found = FindMatchInExpression(declInitValue, isMatchInvocation, matchMembers);
                        if (found is not null)
                        {
                            return found;
                        }
                    }

                    // In C#, the initializer is on each declarator.
                    foreach (var declarator in declaration.Declarators)
                    {
                        if (declarator.Initializer?.Value is { } initValue)
                        {
                            var found = FindMatchInExpression(initValue, isMatchInvocation, matchMembers);
                            if (found is not null)
                            {
                                return found;
                            }
                        }
                    }
                }

                return null;
            }

            // Handle expression statements: Regex.Match(...) or m = Regex.Match(...)
            if (operation is IExpressionStatementOperation exprStatement)
            {
                return FindMatchInExpression(exprStatement.Operation, isMatchInvocation, matchMembers);
            }

            // Handle return statements: return Regex.Match(...)
            if (operation is IReturnOperation returnOp && returnOp.ReturnedValue is not null)
            {
                return FindMatchInExpression(returnOp.ReturnedValue, isMatchInvocation, matchMembers);
            }

            return null;
        }

        /// <summary>
        /// Walks an expression tree looking for a Regex.Match invocation with equivalent arguments.
        /// Walks into member access, indexer access, invocation arguments, assignment right-hand sides,
        /// and conversions — but NOT into lambdas, anonymous functions, or nested blocks.
        /// </summary>
        private static IInvocationOperation? FindMatchInExpression(
            IOperation expression,
            IInvocationOperation isMatchInvocation,
            ImmutableArray<ISymbol> matchMembers)
        {
            expression = expression.WalkDownParentheses().WalkDownConversion();

            // Direct invocation: check if it's a matching Regex.Match call.
            if (expression is IInvocationOperation invocation)
            {
                if (IsMatchingMatchCall(invocation, isMatchInvocation, matchMembers))
                {
                    return invocation;
                }

                // Also check arguments of this invocation (e.g., SomeMethod(Regex.Match(...)))
                // and the instance receiver.
                if (invocation.Instance is not null)
                {
                    var found = FindMatchInExpression(invocation.Instance, isMatchInvocation, matchMembers);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                foreach (var arg in invocation.Arguments)
                {
                    var found = FindMatchInExpression(arg.Value, isMatchInvocation, matchMembers);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;
            }

            // Member access: e.g., Regex.Match(...).Groups[1].Value
            if (expression is IPropertyReferenceOperation propRef)
            {
                return propRef.Instance is not null
                    ? FindMatchInExpression(propRef.Instance, isMatchInvocation, matchMembers)
                    : null;
            }

            // Array/indexer element access
            if (expression is IArrayElementReferenceOperation arrayRef)
            {
                return FindMatchInExpression(arrayRef.ArrayReference, isMatchInvocation, matchMembers);
            }

            // Simple assignment: x = Regex.Match(...)
            if (expression is ISimpleAssignmentOperation assignment)
            {
                return FindMatchInExpression(assignment.Value, isMatchInvocation, matchMembers);
            }

            // Conditional access: Regex.Match(...)?.Groups
            if (expression is IConditionalAccessOperation condAccess)
            {
                return FindMatchInExpression(condAccess.Operation, isMatchInvocation, matchMembers);
            }

            return null;
        }

        /// <summary>
        /// Checks whether a given invocation is a Regex.Match call with arguments semantically
        /// equivalent to the provided IsMatch call.
        /// </summary>
        private static bool IsMatchingMatchCall(
            IInvocationOperation matchInvocation,
            IInvocationOperation isMatchInvocation,
            ImmutableArray<ISymbol> matchMembers)
        {
            // Must be a Regex.Match method
            if (matchInvocation.TargetMethod.Name != "Match" ||
                !matchMembers.Contains(matchInvocation.TargetMethod, SymbolEqualityComparer.Default))
            {
                return false;
            }

            // Both must be static or both instance
            if (isMatchInvocation.TargetMethod.IsStatic != matchInvocation.TargetMethod.IsStatic)
            {
                return false;
            }

            // For instance methods, verify same receiver
            if (!isMatchInvocation.TargetMethod.IsStatic)
            {
                if (!AreOperandsEquivalent(isMatchInvocation.Instance, matchInvocation.Instance))
                {
                    return false;
                }
            }

            // Both must have the same parameter types (same overload shape)
            if (!ParameterTypesMatch(
                    isMatchInvocation.TargetMethod.Parameters,
                    matchInvocation.TargetMethod.Parameters))
            {
                return false;
            }

            // All arguments must be semantically equivalent
            if (isMatchInvocation.Arguments.Length != matchInvocation.Arguments.Length)
            {
                return false;
            }

            // Compare arguments by parameter ordinal rather than array index,
            // because named arguments may reorder the Arguments array.
            foreach (IArgumentOperation isMatchArg in isMatchInvocation.Arguments)
            {
                if (isMatchArg.Parameter is null)
                {
                    return false;
                }

                IArgumentOperation? matchArg = null;
                foreach (IArgumentOperation candidate in matchInvocation.Arguments)
                {
                    if (candidate.Parameter?.Ordinal == isMatchArg.Parameter.Ordinal)
                    {
                        matchArg = candidate;
                        break;
                    }
                }

                if (matchArg is null ||
                    !AreOperandsEquivalent(isMatchArg.Value, matchArg.Value))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether two lists of parameters have the same types in the same order.
        /// </summary>
        private static bool ParameterTypesMatch(
            ImmutableArray<IParameterSymbol> left,
            ImmutableArray<IParameterSymbol> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(left[i].Type, right[i].Type))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether two operations refer to the same value. Only considers stable
        /// operand sources: locals, parameters, constants, and readonly fields.
        /// </summary>
        private static bool AreOperandsEquivalent(IOperation? left, IOperation? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            left = left.WalkDownConversion();
            right = right.WalkDownConversion();

            // Same local variable reference
            if (left is ILocalReferenceOperation leftLocal &&
                right is ILocalReferenceOperation rightLocal)
            {
                return SymbolEqualityComparer.Default.Equals(leftLocal.Local, rightLocal.Local);
            }

            // Same parameter reference
            if (left is IParameterReferenceOperation leftParam &&
                right is IParameterReferenceOperation rightParam)
            {
                return SymbolEqualityComparer.Default.Equals(leftParam.Parameter, rightParam.Parameter);
            }

            // Same constant value
            if (left.ConstantValue.HasValue && right.ConstantValue.HasValue)
            {
                return Equals(left.ConstantValue.Value, right.ConstantValue.Value);
            }

            // Same instance reference (this/Me)
            if (left is IInstanceReferenceOperation && right is IInstanceReferenceOperation)
            {
                return true;
            }

            // Same field reference (readonly fields only for safety)
            if (left is IFieldReferenceOperation leftField &&
                right is IFieldReferenceOperation rightField)
            {
                if (!SymbolEqualityComparer.Default.Equals(leftField.Field, rightField.Field))
                {
                    return false;
                }

                // Only trust readonly/const fields
                if (!leftField.Field.IsReadOnly && !leftField.Field.IsConst)
                {
                    return false;
                }

                // For instance fields, receivers must also be equivalent
                if (!leftField.Field.IsStatic)
                {
                    return AreOperandsEquivalent(leftField.Instance, rightField.Instance);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Collects all local and parameter symbols directly referenced by the invocation's
        /// arguments and instance receiver.
        /// </summary>
        private static void CollectReferencedMutableSymbols(
            IInvocationOperation invocation,
            ref HashSet<ISymbol>? symbols)
        {
            foreach (var arg in invocation.Arguments)
            {
                AddMutableSymbol(arg.Value, ref symbols);
            }

            if (invocation.Instance is not null)
            {
                AddMutableSymbol(invocation.Instance, ref symbols);
            }

            static void AddMutableSymbol(IOperation operation, ref HashSet<ISymbol>? symbols)
            {
                operation = operation.WalkDownConversion();

                if (operation is ILocalReferenceOperation localRef)
                {
                    symbols ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                    symbols.Add(localRef.Local);
                }
                else if (operation is IParameterReferenceOperation paramRef)
                {
                    symbols ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                    symbols.Add(paramRef.Parameter);
                }
                else if (operation is IFieldReferenceOperation fieldRef && fieldRef.Instance is not null)
                {
                    // For h.ReadonlyField, track h so that reassignment of h is detected.
                    AddMutableSymbol(fieldRef.Instance, ref symbols);
                }
            }
        }

        /// <summary>
        /// Checks whether an operation or any of its descendants writes to any of the
        /// given symbols (via assignment, compound assignment, increment/decrement, or
        /// ref/out argument passing). Does not recurse into lambdas or local functions.
        /// </summary>
        private static bool ContainsWriteToSymbols(IOperation operation, HashSet<ISymbol> symbols)
        {
            ISymbol? writtenSymbol = GetWrittenSymbol(operation);
            if (writtenSymbol is not null && symbols.Contains(writtenSymbol))
            {
                return true;
            }

            // Check for ref/out argument passing which can mutate tracked symbols.
            if (operation is IArgumentOperation argOp &&
                argOp.Parameter is not null &&
                argOp.Parameter.RefKind is RefKind.Ref or RefKind.Out)
            {
                var argValue = argOp.Value.WalkDownConversion();
                ISymbol? argSymbol = argValue switch
                {
                    ILocalReferenceOperation localRef => localRef.Local,
                    IParameterReferenceOperation paramRef => paramRef.Parameter,
                    _ => null
                };

                if (argSymbol is not null && symbols.Contains(argSymbol))
                {
                    return true;
                }
            }

            // Check for deconstruction assignments: (x, y) = expr
            // The target tuple elements are written to, but aren't modeled as
            // individual assignment operations.
            if (operation is IDeconstructionAssignmentOperation decon &&
                ContainsTrackedSymbolReference(decon.Target, symbols))
            {
                return true;
            }

            foreach (var child in operation.Children)
            {
                if (child is IAnonymousFunctionOperation or ILocalFunctionOperation)
                {
                    continue;
                }

                if (ContainsWriteToSymbols(child, symbols))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// If the operation is an assignment or increment/decrement targeting a local or parameter,
        /// returns that symbol. Otherwise returns null.
        /// </summary>
        private static ISymbol? GetWrittenSymbol(IOperation operation)
        {
            IOperation? target = operation switch
            {
                ISimpleAssignmentOperation assignment => assignment.Target,
                ICompoundAssignmentOperation compound => compound.Target,
                ICoalesceAssignmentOperation coalesce => coalesce.Target,
                IIncrementOrDecrementOperation incDec => incDec.Target,
                _ => null
            };

            if (target is null)
            {
                return null;
            }

            target = target.WalkDownConversion();

            return target switch
            {
                ILocalReferenceOperation localRef => localRef.Local,
                IParameterReferenceOperation paramRef => paramRef.Parameter,
                _ => null
            };
        }
        /// <summary>
        /// Checks whether an operation tree contains a local or parameter reference
        /// to any of the tracked symbols. Used for deconstruction assignment targets.
        /// </summary>
        private static bool ContainsTrackedSymbolReference(IOperation operation, HashSet<ISymbol> symbols)
        {
            var unwrapped = operation.WalkDownConversion();

            ISymbol? symbol = unwrapped switch
            {
                ILocalReferenceOperation localRef => localRef.Local,
                IParameterReferenceOperation paramRef => paramRef.Parameter,
                _ => null
            };

            if (symbol is not null && symbols.Contains(symbol))
            {
                return true;
            }

            foreach (var child in unwrapped.Children)
            {
                if (ContainsTrackedSymbolReference(child, symbols))
                {
                    return true;
                }
            }

            return false;
        }
    }
}