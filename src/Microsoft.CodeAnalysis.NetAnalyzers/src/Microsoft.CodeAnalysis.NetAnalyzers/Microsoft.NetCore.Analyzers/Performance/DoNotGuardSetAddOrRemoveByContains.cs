// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA1868: <inheritdoc cref="@DoNotGuardSetAddOrRemoveByContainsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotGuardSetAddOrRemoveByContains : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1868";

        private const string Contains = nameof(Contains);
        private const string Add = nameof(Add);
        private const string Remove = nameof(Remove);

        // Build custom format instead of CSharpShortErrorMessageFormat/VisualBasicShortErrorMessageFormat to prevent unhelpful messages for VB.
        private static readonly SymbolDisplayFormat s_symbolDisplayFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType)
            .WithGenericsOptions(SymbolDisplayGenericsOptions.None)
            .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeContainingType)
            .WithKindOptions(SymbolDisplayKindOptions.None);

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotGuardSetAddOrRemoveByContainsTitle)),
            CreateLocalizableResourceString(nameof(DoNotGuardSetAddOrRemoveByContainsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(DoNotGuardSetAddOrRemoveByContainsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
            {
                return;
            }

            context.RegisterOperationAction(OnConditional, OperationKind.Conditional);

            void OnConditional(OperationAnalysisContext context)
            {
                var conditional = (IConditionalOperation)context.Operation;

                if (!symbols.HasApplicableContainsMethod(conditional.Condition, out var containsInvocation, out bool containsNegated) ||
                    !symbols.HasApplicableAddOrRemoveMethod(conditional, containsNegated, out var addOrRemoveInvocation) ||
                    !AreInvocationsOnSameInstance(containsInvocation, addOrRemoveInvocation) ||
                    !AreInvocationArgumentsEqual(containsInvocation, addOrRemoveInvocation))
                {
                    return;
                }

                using var locations = ArrayBuilder<Location>.GetInstance(2);
                locations.Add(conditional.Syntax.GetLocation());
                locations.Add(addOrRemoveInvocation.Syntax.Parent!.GetLocation());

                context.ReportDiagnostic(containsInvocation.CreateDiagnostic(
                    Rule,
                    additionalLocations: locations.ToImmutable(),
                    properties: null,
                    addOrRemoveInvocation.TargetMethod.ToDisplayString(s_symbolDisplayFormat),
                    containsInvocation.TargetMethod.ToDisplayString(s_symbolDisplayFormat)));
            }
        }

        private static bool AreInvocationsOnSameInstance(IInvocationOperation invocation1, IInvocationOperation invocation2)
        {
            return (invocation1.Instance, invocation2.Instance) switch
            {
                (IFieldReferenceOperation fieldRef1, IFieldReferenceOperation fieldRef2) => fieldRef1.Member == fieldRef2.Member,
                (IPropertyReferenceOperation propRef1, IPropertyReferenceOperation propRef2) => propRef1.Member == propRef2.Member,
                (IParameterReferenceOperation paramRef1, IParameterReferenceOperation paramRef2) => paramRef1.Parameter == paramRef2.Parameter,
                (ILocalReferenceOperation localRef1, ILocalReferenceOperation localRef2) => localRef1.Local == localRef2.Local,
                _ => false,
            };
        }

        // Checks if invocation argument values are equal
        //   1. Not equal: Contains(item) != Add(otherItem), Contains("const") != Add("other const")
        //   2. Identical: Contains(item) == Add(item), Contains("const") == Add("const")
        private static bool AreInvocationArgumentsEqual(IInvocationOperation invocation1, IInvocationOperation invocation2)
        {
            return IsArgumentValueEqual(invocation1.Arguments[0].Value, invocation2.Arguments[0].Value);
        }

        private static bool IsArgumentValueEqual(IOperation targetArg, IOperation valueArg)
        {
            // Check if arguments are identical constant/local/parameter/field reference operations.
            if (targetArg.Kind != valueArg.Kind)
            {
                return false;
            }

            if (targetArg.ConstantValue.HasValue != valueArg.ConstantValue.HasValue)
            {
                return false;
            }

            if (targetArg.ConstantValue.HasValue)
            {
                return Equals(targetArg.ConstantValue.Value, valueArg.ConstantValue.Value);
            }

            return targetArg switch
            {
                ILocalReferenceOperation targetLocalReference =>
                    SymbolEqualityComparer.Default.Equals(targetLocalReference.Local, ((ILocalReferenceOperation)valueArg).Local),
                IParameterReferenceOperation targetParameterReference =>
                    SymbolEqualityComparer.Default.Equals(targetParameterReference.Parameter, ((IParameterReferenceOperation)valueArg).Parameter),
                IFieldReferenceOperation fieldParameterReference =>
                    SymbolEqualityComparer.Default.Equals(fieldParameterReference.Member, ((IFieldReferenceOperation)valueArg).Member),
                _ => false,
            };
        }

        private static bool DoesImplementInterfaceMethod(IMethodSymbol? method, IMethodSymbol? interfaceMethod)
        {
            if (method is null || interfaceMethod is null || method.Parameters.Length != 1)
            {
                return false;
            }

            var typedInterface = interfaceMethod.ContainingType.Construct(method.Parameters[0].Type);
            var typedInterfaceMethod = typedInterface.GetMembers(interfaceMethod.Name).FirstOrDefault();

            // Also check against all original definitions to also cover external interface implementations
            return SymbolEqualityComparer.Default.Equals(method, typedInterfaceMethod) ||
                method.GetOriginalDefinitions().Any(definition => SymbolEqualityComparer.Default.Equals(definition, typedInterfaceMethod));
        }

        internal sealed class RequiredSymbols
        {
            private RequiredSymbols(IMethodSymbol addMethod, IMethodSymbol removeMethod, IMethodSymbol containsMethod, IMethodSymbol? addMethodImmutableSet, IMethodSymbol? removeMethodImmutableSet, IMethodSymbol? containsMethodImmutableSet)
            {
                AddMethod = addMethod;
                RemoveMethod = removeMethod;
                ContainsMethod = containsMethod;
                AddMethodImmutableSet = addMethodImmutableSet;
                RemoveMethodImmutableSet = removeMethodImmutableSet;
                ContainsMethodImmutableSet = containsMethodImmutableSet;
            }

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var typeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
                var iSetType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericISet1);
                var iCollectionType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericICollection1);

                if (iSetType is null || iCollectionType is null)
                {
                    return false;
                }

                IMethodSymbol? addMethod = iSetType.GetMembers(Add).OfType<IMethodSymbol>().FirstOrDefault();
                IMethodSymbol? removeMethod = null;
                IMethodSymbol? containsMethod = null;

                foreach (var method in iCollectionType.GetMembers().OfType<IMethodSymbol>())
                {
                    switch (method.Name)
                    {
                        case Remove: removeMethod = method; break;
                        case Contains: containsMethod = method; break;
                    }
                }

                if (addMethod is null || removeMethod is null || containsMethod is null)
                {
                    return false;
                }

                IMethodSymbol? addMethodImmutableSet = null;
                IMethodSymbol? removeMethodImmutableSet = null;
                IMethodSymbol? containsMethodImmutableSet = null;

                // The methods from IImmutableSet are optional and will not lead to a code fix.
                var iImmutableSetType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsImmutableIImmutableSet1);

                if (iImmutableSetType is not null)
                {
                    foreach (var method in iImmutableSetType.GetMembers().OfType<IMethodSymbol>())
                    {
                        switch (method.Name)
                        {
                            case Add: addMethodImmutableSet = method; break;
                            case Remove: removeMethodImmutableSet = method; break;
                            case Contains: containsMethodImmutableSet = method; break;
                        }
                    }
                }

                symbols = new RequiredSymbols(
                    addMethod, removeMethod, containsMethod,
                    addMethodImmutableSet, removeMethodImmutableSet, containsMethodImmutableSet);

                return true;
            }

            // A condition contains an applicable 'Contains' method in the following cases:
            //   1. The condition contains only the 'Contains' invocation.
            //   2. The condition contains a unary not operation where the operand is a 'Contains' invocation.
            //
            // In all cases, the invocation must implement either 'ICollection.Contains' or 'IImmutableSet.Contains'.
            public bool HasApplicableContainsMethod(
                IOperation condition,
                [NotNullWhen(true)] out IInvocationOperation? containsInvocation,
                out bool containsNegated)
            {
                containsNegated = false;
                containsInvocation = null;

                switch (condition.WalkDownParentheses())
                {
                    case IInvocationOperation invocation:
                        containsInvocation = invocation;
                        break;
                    case IUnaryOperation unaryOperation when unaryOperation.OperatorKind == UnaryOperatorKind.Not && unaryOperation.Operand is IInvocationOperation operand:
                        containsNegated = true;
                        containsInvocation = operand;
                        break;
                    default:
                        return false;
                }

                return DoesImplementInterfaceMethod(containsInvocation.TargetMethod, ContainsMethod) ||
                    DoesImplementInterfaceMethod(containsInvocation.TargetMethod, ContainsMethodImmutableSet);
            }

            // A conditional contains an applicable 'Add' or 'Remove' method if the first operation of WhenTrue or WhenFalse satisfies one of the following:
            //   1. The operation is an invocation of 'Add' or 'Remove'.
            //   2. The operation is either a simple assignment or an expression statement.
            //      In this case the child statements are checked if they contain an invocation of 'Add' or 'Remove'.
            //   3. The operation is a variable group declaration.
            //      In this case the descendants are checked if they contain an invocation of 'Add' or 'Remove'.
            // OR when the WhenTrue or WhenFalse is a InvocationOperation (in the case of a ternary operator).
            //
            // In all cases, the invocation must implement either
            //   1. 'ISet.Add' or 'IImmutableSet.Add'
            //   2. 'ICollection.Remove' or 'IImmutableSet.Remove'
            public bool HasApplicableAddOrRemoveMethod(
                IConditionalOperation conditional,
                bool containsNegated,
                [NotNullWhen(true)] out IInvocationOperation? addOrRemoveInvocation)
            {
                addOrRemoveInvocation = GetApplicableAddOrRemove(conditional.WhenTrue, extractAdd: containsNegated);

                if (addOrRemoveInvocation is null)
                {
                    addOrRemoveInvocation = GetApplicableAddOrRemove(conditional.WhenFalse, extractAdd: !containsNegated);
                }

                return addOrRemoveInvocation is not null;
            }

            private IInvocationOperation? GetApplicableAddOrRemove(IOperation? operation, bool extractAdd)
            {
                if (operation is IInvocationOperation ternaryInvocation)
                {
                    if ((extractAdd && IsAnyAddMethod(ternaryInvocation.TargetMethod)) ||
                        (!extractAdd && IsAnyRemoveMethod(ternaryInvocation.TargetMethod)))
                    {
                        return ternaryInvocation;
                    }
                }

                var firstChildOperation = operation?.Children.FirstOrDefault();

                switch (firstChildOperation)
                {
                    case IInvocationOperation invocation:
                        if ((extractAdd && IsAnyAddMethod(invocation.TargetMethod)) ||
                            (!extractAdd && IsAnyRemoveMethod(invocation.TargetMethod)))
                        {
                            return invocation;
                        }

                        break;

                    case ISimpleAssignmentOperation:
                    case IExpressionStatementOperation:
                        var firstChildAddOrRemove = firstChildOperation.Children
                            .OfType<IInvocationOperation>()
                            .FirstOrDefault(i => extractAdd ?
                                IsAnyAddMethod(i.TargetMethod) :
                                IsAnyRemoveMethod(i.TargetMethod));

                        if (firstChildAddOrRemove != null)
                        {
                            return firstChildAddOrRemove;
                        }

                        break;

                    case IVariableDeclarationGroupOperation variableDeclarationGroup:
                        var firstDescendantAddOrRemove = firstChildOperation.Descendants()
                            .OfType<IInvocationOperation>()
                            .FirstOrDefault(i => extractAdd ?
                                IsAnyAddMethod(i.TargetMethod) :
                                IsAnyRemoveMethod(i.TargetMethod));

                        if (firstDescendantAddOrRemove != null)
                        {
                            return firstDescendantAddOrRemove;
                        }

                        break;
                }

                return null;
            }

            private bool IsAnyAddMethod(IMethodSymbol method)
            {
                return DoesImplementInterfaceMethod(method, AddMethod) ||
                    DoesImplementInterfaceMethod(method, AddMethodImmutableSet);
            }

            private bool IsAnyRemoveMethod(IMethodSymbol method)
            {
                return DoesImplementInterfaceMethod(method, RemoveMethod) ||
                    DoesImplementInterfaceMethod(method, RemoveMethodImmutableSet);
            }

            public IMethodSymbol AddMethod { get; }
            public IMethodSymbol RemoveMethod { get; }
            public IMethodSymbol ContainsMethod { get; }
            public IMethodSymbol? AddMethodImmutableSet { get; }
            public IMethodSymbol? RemoveMethodImmutableSet { get; }
            public IMethodSymbol? ContainsMethodImmutableSet { get; }
        }
    }
}
