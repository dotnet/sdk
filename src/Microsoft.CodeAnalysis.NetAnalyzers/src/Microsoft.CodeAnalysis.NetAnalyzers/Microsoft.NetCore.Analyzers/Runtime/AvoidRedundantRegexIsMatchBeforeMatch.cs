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

                    // Check if condition is Regex.IsMatch call
                    if (!IsRegexIsMatchCall(conditional.Condition, regexIsMatchSymbols, out var isMatchCall))
                    {
                        return;
                    }

                    // Look for Regex.Match calls in the when-true branch
                    var matchCalls = GetRegexMatchCalls(conditional.WhenTrue, regexMatchSymbols);

                    // Check if any Match call has the same arguments as the IsMatch call
                    foreach (var matchCall in matchCalls)
                    {
                        if (AreInvocationArgumentsEqual(isMatchCall, matchCall) &&
                            AreInvocationsOnSameInstance(isMatchCall, matchCall))
                        {
                            context.ReportDiagnostic(isMatchCall.CreateDiagnostic(Rule));
                            return;
                        }
                    }
                }, OperationKind.Conditional);
            });
        }

        private static bool IsRegexIsMatchCall(IOperation condition, ImmutableArray<IMethodSymbol> regexIsMatchSymbols, out IInvocationOperation isMatchCall)
        {
            // Handle unwrapping of conversions and parenthesized expressions
            var unwrapped = condition.WalkDownConversion();

            if (unwrapped is IInvocationOperation invocation &&
                regexIsMatchSymbols.Contains(invocation.TargetMethod, SymbolEqualityComparer.Default))
            {
                isMatchCall = invocation;
                return true;
            }

            isMatchCall = null!;
            return false;
        }

        private static ImmutableArray<IInvocationOperation> GetRegexMatchCalls(IOperation operation, ImmutableArray<IMethodSymbol> regexMatchSymbols)
        {
            var builder = ImmutableArray.CreateBuilder<IInvocationOperation>();
            GetRegexMatchCallsRecursive(operation, regexMatchSymbols, builder);
            return builder.ToImmutable();
        }

        private static void GetRegexMatchCallsRecursive(IOperation operation, ImmutableArray<IMethodSymbol> regexMatchSymbols, ImmutableArray<IInvocationOperation>.Builder builder)
        {
            if (operation is IInvocationOperation invocation &&
                regexMatchSymbols.Contains(invocation.TargetMethod, SymbolEqualityComparer.Default))
            {
                builder.Add(invocation);
            }

            foreach (var child in operation.Children)
            {
                GetRegexMatchCallsRecursive(child, regexMatchSymbols, builder);
            }
        }

        private static bool AreInvocationsOnSameInstance(IInvocationOperation invocation1, IInvocationOperation invocation2)
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

            return (instance1, instance2) switch
            {
                (IFieldReferenceOperation fieldRef1, IFieldReferenceOperation fieldRef2) => SymbolEqualityComparer.Default.Equals(fieldRef1.Member, fieldRef2.Member),
                (IPropertyReferenceOperation propRef1, IPropertyReferenceOperation propRef2) => SymbolEqualityComparer.Default.Equals(propRef1.Member, propRef2.Member),
                (IParameterReferenceOperation paramRef1, IParameterReferenceOperation paramRef2) => SymbolEqualityComparer.Default.Equals(paramRef1.Parameter, paramRef2.Parameter),
                (ILocalReferenceOperation localRef1, ILocalReferenceOperation localRef2) => SymbolEqualityComparer.Default.Equals(localRef1.Local, localRef2.Local),
                _ => false,
            };
        }

        private static bool AreInvocationArgumentsEqual(IInvocationOperation invocation1, IInvocationOperation invocation2)
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
            }

            return true;
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
