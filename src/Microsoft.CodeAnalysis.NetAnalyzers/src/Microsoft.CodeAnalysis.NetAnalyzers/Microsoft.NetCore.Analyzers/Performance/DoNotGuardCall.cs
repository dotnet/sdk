// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1853: <inheritdoc cref="DoNotGuardDictionaryRemoveByContainsKeyTitle"/>
    /// CA1868: <inheritdoc cref="DoNotGuardSetAddOrRemoveByContainsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotGuardCallAnalyzer : DiagnosticAnalyzer
    {
        internal const string DoNotGuardDictionaryRemoveByContainsKeyRuleId = "CA1853";
        internal const string DoNotGuardSetAddOrRemoveByContainsRuleId = "CA1868";

        internal static readonly DiagnosticDescriptor DoNotGuardDictionaryRemoveByContainsKeyRule = DiagnosticDescriptorHelper.Create(
            DoNotGuardDictionaryRemoveByContainsKeyRuleId,
            CreateLocalizableResourceString(nameof(DoNotGuardDictionaryRemoveByContainsKeyTitle)),
            CreateLocalizableResourceString(nameof(DoNotGuardDictionaryRemoveByContainsKeyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(DoNotGuardDictionaryRemoveByContainsKeyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor DoNotGuardSetAddOrRemoveByContainsRule = DiagnosticDescriptorHelper.Create(
            DoNotGuardSetAddOrRemoveByContainsRuleId,
            CreateLocalizableResourceString(nameof(DoNotGuardSetAddOrRemoveByContainsTitle)),
            CreateLocalizableResourceString(nameof(DoNotGuardSetAddOrRemoveByContainsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(DoNotGuardSetAddOrRemoveByContainsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // Build custom format instead of CSharpShortErrorMessageFormat/VisualBasicShortErrorMessageFormat to prevent unhelpful messages for VB.
        private static readonly SymbolDisplayFormat s_symbolDisplayFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType)
            .WithGenericsOptions(SymbolDisplayGenericsOptions.None)
            .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeContainingType)
            .WithKindOptions(SymbolDisplayKindOptions.None);

        private static readonly ImmutableHashSet<GuardedCallContext> s_guardedCallContexts = ImmutableHashSet.Create(
            new GuardedCallContext(
                DoNotGuardDictionaryRemoveByContainsKeyRule,
                new ConditionMethodContext(WellKnownTypeNames.SystemCollectionsGenericIDictionary2, "ContainsKey"),
                ImmutableArray.Create(
                    new GuardedMethodContext(WellKnownTypeNames.SystemCollectionsGenericIDictionary2, "Remove", 1, ExpectsConditionNegated: false),
                    new GuardedMethodContext(WellKnownTypeNames.SystemCollectionsGenericDictionary2, "Remove", 2, ExpectsConditionNegated: false))),
            new GuardedCallContext(
                DoNotGuardDictionaryRemoveByContainsKeyRule,
                new ConditionMethodContext(WellKnownTypeNames.SystemCollectionsGenericIReadOnlyDictionary2, "ContainsKey"),
                ImmutableArray.Create(
                    new GuardedMethodContext(WellKnownTypeNames.SystemCollectionsImmutableIImmutableDictionary2, "Remove", 1, ExpectsConditionNegated: false))),
            new GuardedCallContext(
                DoNotGuardSetAddOrRemoveByContainsRule,
                new ConditionMethodContext(WellKnownTypeNames.SystemCollectionsGenericICollection1, "Contains"),
                ImmutableArray.Create(
                    new GuardedMethodContext(WellKnownTypeNames.SystemCollectionsGenericISet1, "Add", 1, ExpectsConditionNegated: true),
                    new GuardedMethodContext(WellKnownTypeNames.SystemCollectionsGenericICollection1, "Remove", 1, ExpectsConditionNegated: false))),
            new GuardedCallContext(
                DoNotGuardSetAddOrRemoveByContainsRule,
                new ConditionMethodContext(WellKnownTypeNames.SystemCollectionsImmutableIImmutableSet1, "Contains"),
                ImmutableArray.Create(
                    new GuardedMethodContext(WellKnownTypeNames.SystemCollectionsImmutableIImmutableSet1, "Add", 1, ExpectsConditionNegated: true),
                    new GuardedMethodContext(WellKnownTypeNames.SystemCollectionsImmutableIImmutableSet1, "Remove", 1, ExpectsConditionNegated: false))));

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DoNotGuardDictionaryRemoveByContainsKeyRule, DoNotGuardSetAddOrRemoveByContainsRule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            foreach (var guardedCallContext in s_guardedCallContexts)
            {
                if (!GuardedCallSymbols.TryGetSymbols(context.Compilation, guardedCallContext, out var symbols))
                {
                    return;
                }

                context.RegisterOperationAction(OnConditional, OperationKind.Conditional);

                void OnConditional(OperationAnalysisContext context)
                {
                    var conditional = (IConditionalOperation)context.Operation;

                    if (!symbols.HasApplicableConditionInvocation(conditional.Condition, out var conditionInvocation, out bool containsNegated) ||
                        !symbols.HasApplicableGuardedInvocation(conditional, containsNegated, out var guardedInvocation) ||
                        !AreInvocationsOnSameInstance(conditionInvocation, guardedInvocation) ||
                        !AreInvocationArgumentsEqual(conditionInvocation, guardedInvocation))
                    {
                        return;
                    }

                    using var locations = ArrayBuilder<Location>.GetInstance(2);
                    locations.Add(conditional.Syntax.GetLocation());
                    locations.Add(guardedInvocation.Syntax.Parent!.GetLocation());

                    context.ReportDiagnostic(conditionInvocation.CreateDiagnostic(
                        guardedCallContext.Rule,
                        additionalLocations: locations.ToImmutable(),
                        properties: null,
                        guardedInvocation.TargetMethod.ToDisplayString(s_symbolDisplayFormat),
                        conditionInvocation.TargetMethod.ToDisplayString(s_symbolDisplayFormat)));
                }
            }
        }

        private static bool AreInvocationsOnSameInstance(IInvocationOperation invocation1, IInvocationOperation invocation2)
        {
            return (invocation1.GetInstance()?.WalkDownConversion(), invocation2.GetInstance()?.WalkDownConversion()) switch
            {
                (IFieldReferenceOperation fieldRef1, IFieldReferenceOperation fieldRef2) => fieldRef1.Member == fieldRef2.Member,
                (IPropertyReferenceOperation propRef1, IPropertyReferenceOperation propRef2) => propRef1.Member == propRef2.Member,
                (IParameterReferenceOperation paramRef1, IParameterReferenceOperation paramRef2) => paramRef1.Parameter == paramRef2.Parameter,
                (ILocalReferenceOperation localRef1, ILocalReferenceOperation localRef2) => localRef1.Local == localRef2.Local,
                _ => false,
            };
        }

        private static bool AreInvocationArgumentsEqual(IInvocationOperation invocation1, IInvocationOperation invocation2)
        {
            return AreArgumentValuesEqual(GetFirstNonInstanceArgument(invocation1)?.Value, GetFirstNonInstanceArgument(invocation2)?.Value);
        }

        private static IArgumentOperation? GetFirstNonInstanceArgument(IInvocationOperation invocation)
        {
            // Return the second argument in parameter order for extension methods with no instance
            var parameterIndex = invocation.IsExtensionMethodAndHasNoInstance() ? 1 : 0;
            invocation.Arguments.TryGetArgumentForParameterAtIndex(parameterIndex, out var argument);

            return argument;
        }

        // Check if arguments are identical constant/local/parameter/field reference operations.
        private static bool AreArgumentValuesEqual(IOperation? argumentValue1, IOperation? argumentValue2)
        {
            if (argumentValue1 is null || argumentValue2 is null ||
                argumentValue1.Kind != argumentValue2.Kind ||
                argumentValue1.ConstantValue.HasValue != argumentValue2.ConstantValue.HasValue)
            {
                return false;
            }

            if (argumentValue1.ConstantValue.HasValue)
            {
                return Equals(argumentValue1.ConstantValue.Value, argumentValue2.ConstantValue.Value);
            }

            return argumentValue1 switch
            {
                ILocalReferenceOperation targetLocalReference =>
                    SymbolEqualityComparer.Default.Equals(targetLocalReference.Local, ((ILocalReferenceOperation)argumentValue2).Local),
                IParameterReferenceOperation targetParameterReference =>
                    SymbolEqualityComparer.Default.Equals(targetParameterReference.Parameter, ((IParameterReferenceOperation)argumentValue2).Parameter),
                IFieldReferenceOperation fieldParameterReference =>
                    SymbolEqualityComparer.Default.Equals(fieldParameterReference.Member, ((IFieldReferenceOperation)argumentValue2).Member),
                _ => false,
            };
        }

        private static bool DoesImplementInterfaceMethod(IMethodSymbol? method, IMethodSymbol? interfaceMethod, Compilation compilation)
        {
            if (method is null || interfaceMethod is null)
            {
                return false;
            }

            // This also covers implementation through extension methods, like CollectionExtensions.Remove(IDictionary<TKey, TValue>, TKey, out TValue)
            if (method.IsExtensionMethod)
            {
                if (method.ReducedFrom is null && method.Parameters.Length > 0)
                {
                    method = method.ReduceExtensionMethod(method.Parameters[0].Type) ?? method;
                }

                return method.Name.Equals(interfaceMethod.Name, StringComparison.Ordinal) &&
                    method.OriginalDefinition.ParametersAreSame(interfaceMethod) &&
                    method.OriginalDefinition.ReturnType.IsAssignableTo(interfaceMethod.ReturnType, compilation);
            }

            var originalDefinitions = method.GetOriginalDefinitions().WhereAsArray(m => method.ReturnType.IsAssignableTo(m.ReturnType, compilation));

            // For both branches use the original definition as the interface method is non-constructed.
            if (originalDefinitions.Length == 0)
            {
                // This branch is for access through interface types.
                return SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, interfaceMethod);
            }
            else
            {
                return originalDefinitions.Any(o => SymbolEqualityComparer.Default.Equals(o.OriginalDefinition, interfaceMethod));
            }
        }

        private record ConditionMethodContext(string ContainingTypeName, string Name);
        private record GuardedMethodContext(string ContainingTypeName, string Name, int ParameterCount, bool ExpectsConditionNegated);
        private record GuardedCallContext(DiagnosticDescriptor Rule, ConditionMethodContext ConditionMethodContext, ImmutableArray<GuardedMethodContext> GuardedMethodContexts);
        private record GuardedMethod(IMethodSymbol Symbol, bool ExpectsConditionNegated);

        private class GuardedCallSymbols
        {
            private readonly Compilation _compilation;
            private readonly IMethodSymbol _conditionMethod;
            private readonly ImmutableArray<GuardedMethod> _guardedMethods;

            private GuardedCallSymbols(Compilation compilation, IMethodSymbol conditionMethod, ImmutableArray<GuardedMethod> guardedMethods)
            {
                _compilation = compilation;
                _conditionMethod = conditionMethod;
                _guardedMethods = guardedMethods;
            }

            public static bool TryGetSymbols(Compilation compilation, GuardedCallContext context, [NotNullWhen(true)] out GuardedCallSymbols? symbols)
            {
                symbols = default;

                var typeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
                var conditionMethodType = typeProvider.GetOrCreateTypeByMetadataName(context.ConditionMethodContext.ContainingTypeName);
                var conditionMethod = conditionMethodType?
                    .GetMembers(context.ConditionMethodContext.Name)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault();

                if (conditionMethodType is null || conditionMethod is null)
                {
                    return false;
                }

                var guardedMethodsBuilder = ImmutableArray.CreateBuilder<GuardedMethod>();

                foreach (var guardedMethodContext in context.GuardedMethodContexts)
                {
                    var guardedMethodType = typeProvider.GetOrCreateTypeByMetadataName(guardedMethodContext.ContainingTypeName);
                    var guardedMethod = guardedMethodType?
                        .GetMembers(guardedMethodContext.Name)
                        .OfType<IMethodSymbol>()
                        .Where(m => m.Parameters.Length == guardedMethodContext.ParameterCount)
                        .FirstOrDefault();

                    if (guardedMethodType is null || guardedMethod is null)
                    {
                        return false;
                    }

                    guardedMethodsBuilder.Add(new GuardedMethod(guardedMethod, guardedMethodContext.ExpectsConditionNegated));
                }

                symbols = new GuardedCallSymbols(compilation, conditionMethod, guardedMethodsBuilder.ToImmutable());

                return true;
            }

            // A condition contains an applicable invocation if one of the following is true:
            //   1. The condition contains only the invocation and the target method implements the condition method.
            //   2. The condition contains an unary not operation where the operand is an invocation that satisfies the first case.
            public bool HasApplicableConditionInvocation(
                IOperation condition,
                [NotNullWhen(true)] out IInvocationOperation? conditionInvocation,
                out bool conditionNegated)
            {
                conditionNegated = false;
                conditionInvocation = null;

                switch (condition.WalkDownParentheses())
                {
                    case IInvocationOperation invocation:
                        conditionInvocation = invocation;
                        break;
                    case IUnaryOperation unaryOperation when unaryOperation.OperatorKind == UnaryOperatorKind.Not && unaryOperation.Operand is IInvocationOperation operand:
                        conditionNegated = true;
                        conditionInvocation = operand;
                        break;
                    default:
                        return false;
                }

                return DoesImplementInterfaceMethod(conditionInvocation.TargetMethod, _conditionMethod, _compilation);
            }

            // A conditional contains an applicable guarded invocation if the first operation of WhenTrue or WhenFalse satisfies one of the following:
            //   1. The operation is a guarded invocation itself.
            //   2. The operation is either a simple assignment or an expression statement.
            //      In this case the child statements are checked if they contain a guarded invocation.
            //   3. The operation is a variable group declaration.
            //      In this case the descendants are checked if they contain a guarded invocation.
            // OR when the WhenTrue or WhenFalse is an InvocationOperation (in the case of a ternary operator).
            //
            // In all cases, the target method must implement any guarded method and the condition negation must match the expected (or differ for the when false case).
            public bool HasApplicableGuardedInvocation(
                IConditionalOperation conditional,
                bool conditionNegated,
                [NotNullWhen(true)] out IInvocationOperation? guardedInvocation)
            {
                guardedInvocation = GetApplicableGuardedInvocation(conditional.WhenTrue, conditionNegated);

                if (guardedInvocation is null)
                {
                    guardedInvocation = GetApplicableGuardedInvocation(conditional.WhenFalse, !conditionNegated);
                }

                return guardedInvocation is not null;
            }

            private IInvocationOperation? GetApplicableGuardedInvocation(IOperation? operation, bool conditionNegated)
            {
                if (operation is IInvocationOperation ternaryInvocation)
                {
                    if (IsAnyGuardedMethod(ternaryInvocation.TargetMethod, conditionNegated))
                    {
                        return ternaryInvocation;
                    }
                }

                var firstChildOperation = operation?.Children.FirstOrDefault();

                switch (firstChildOperation)
                {
                    case IInvocationOperation invocation:
                        if (IsAnyGuardedMethod(invocation.TargetMethod, conditionNegated))
                        {
                            return invocation;
                        }

                        break;

                    case ISimpleAssignmentOperation:
                    case IExpressionStatementOperation:
                        var firstChildAddOrRemove = firstChildOperation.Children
                            .OfType<IInvocationOperation>()
                            .FirstOrDefault(i => IsAnyGuardedMethod(i.TargetMethod, conditionNegated));

                        if (firstChildAddOrRemove != null)
                        {
                            return firstChildAddOrRemove;
                        }

                        break;

                    case IVariableDeclarationGroupOperation variableDeclarationGroup:
                        var firstDescendantAddOrRemove = firstChildOperation.Descendants()
                            .OfType<IInvocationOperation>()
                            .FirstOrDefault(i => IsAnyGuardedMethod(i.TargetMethod, conditionNegated));

                        if (firstDescendantAddOrRemove != null)
                        {
                            return firstDescendantAddOrRemove;
                        }

                        break;
                }

                return null;
            }

            private bool IsAnyGuardedMethod(IMethodSymbol method, bool conditionNegated)
            {
                return _guardedMethods.Any(m => m.ExpectsConditionNegated == conditionNegated && DoesImplementInterfaceMethod(method, m.Symbol, _compilation));
            }
        }
    }
}
