// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Performance
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer : DiagnosticAnalyzer
    {
        internal const string PreferTryGetValueRuleId = "CA1854";
        internal const string PreferTryAddRuleId = "CA1864";

        internal const string Add = nameof(IDictionary<dynamic, dynamic>.Add);
        private const string TryAdd = nameof(TryAdd);
        private const string ContainsKey = nameof(IDictionary<dynamic, dynamic>.ContainsKey);
        private const string Remove = nameof(IDictionary<dynamic, dynamic>.Remove);
        private const string Clear = nameof(IDictionary<dynamic, dynamic>.Clear);

        internal static readonly DiagnosticDescriptor PreferTryGetValueDiagnostic = DiagnosticDescriptorHelper.Create(
            PreferTryGetValueRuleId,
            CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueTitle)),
            CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        private static readonly DiagnosticDescriptor PreferTryAddDiagnostic = DiagnosticDescriptorHelper.Create(
            PreferTryAddRuleId,
            CreateLocalizableResourceString(nameof(PreferDictionaryTryAddTitle)),
            CreateLocalizableResourceString(nameof(PreferDictionaryTryAddMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferDictionaryTryAddDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(PreferTryGetValueDiagnostic, PreferTryAddDiagnostic);

        private struct DictionaryUsageContext : System.IEquatable<DictionaryUsageContext>
        {
            public DictionaryUsageContext(IOperation dictionaryReference, IOperation containsKeyArgumentReference, IMethodSymbol addSymbol)
            {
                DictionaryReference = dictionaryReference;
                ContainsKeyArgumentReference = containsKeyArgumentReference;
                AddSymbol = addSymbol;

                while (dictionaryReference is IArrayElementReferenceOperation a)
                {
                    AdditionalArrayIndexReferences = AdditionalArrayIndexReferences.AddRange(a.Indices);
                    dictionaryReference = a.ArrayReference;
                }
            }

            public readonly bool Equals(DictionaryUsageContext other)
            {
                return Equals(_usageLocations, other._usageLocations) &&
                       DictionaryReference.Equals(other.DictionaryReference) &&
                       ContainsKeyArgumentReference.Equals(other.ContainsKeyArgumentReference) &&
                       AddSymbol.Equals(other.AddSymbol, SymbolEqualityComparer.Default) &&
                       Equals(SetterLocation, other.SetterLocation) &&
                       Equals(AdditionalArrayIndexReferences, other.AdditionalArrayIndexReferences);
            }

            public override readonly bool Equals(object? obj)
            {
                return obj is DictionaryUsageContext other && Equals(other);
            }

            public override readonly int GetHashCode()
            {
                return RoslynHashCode.Combine(_usageLocations, DictionaryReference, ContainsKeyArgumentReference, AddSymbol, SetterLocation, AdditionalArrayIndexReferences);
            }

            public static bool operator ==(DictionaryUsageContext left, DictionaryUsageContext right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(DictionaryUsageContext left, DictionaryUsageContext right)
            {
                return !left.Equals(right);
            }

            public IOperation DictionaryReference { get; }

            public IOperation ContainsKeyArgumentReference { get; }

            public IMethodSymbol AddSymbol { get; }

            public ImmutableArray<IOperation> AdditionalArrayIndexReferences { get; } = ImmutableArray<IOperation>.Empty;

            public ImmutableArray<Location>.Builder UsageLocations
            {
                get
                {
                    _usageLocations ??= ImmutableArray.CreateBuilder<Location>();

                    return _usageLocations;
                }
            }

            public Location? SetterLocation { get; internal set; }

            private ImmutableArray<Location>.Builder? _usageLocations;
        }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!TryGetDictionaryTypeAndMembers(context.Compilation, out var iDictionaryType, out var containsKeySymbol, out var addSymbol))
            {
                return;
            }

            context.RegisterOperationAction(ctx => OnInvocationOperation(iDictionaryType, containsKeySymbol, addSymbol, ctx), OperationKind.Invocation);
        }

        private static void OnInvocationOperation(INamedTypeSymbol iDictionaryType, IMethodSymbol containsKeySymbol, IMethodSymbol addSymbol, OperationAnalysisContext context)
        {
            var containsOperation = (IInvocationOperation)context.Operation;
            if (!IsContainsKeyMethod(containsOperation.TargetMethod, containsKeySymbol))
            {
                return;
            }

            var suspectedDictionaryType = containsOperation.GetReceiverType(context.Compilation, true, context.CancellationToken);
            if (!IsDictionaryType(suspectedDictionaryType, iDictionaryType))
            {
                return;
            }

            ReportGuardedDictionaryPattern(SearchContext.Indexer, PreferTryGetValueDiagnostic);
            ReportGuardedDictionaryPattern(SearchContext.AddMethod, PreferTryAddDiagnostic);

            void ReportGuardedDictionaryPattern(SearchContext searchContext, DiagnosticDescriptor diagnosticDescriptor)
            {
                if (searchContext == SearchContext.AddMethod && suspectedDictionaryType.GetMembers(TryAdd).IsEmpty)
                {
                    return;
                }

                var usageContext = new DictionaryUsageContext(containsOperation.Instance!, containsOperation.Arguments[0].Value, addSymbol);
                if (!GetParentConditionalOperation(containsOperation, ref usageContext, searchContext, out var conditionalOperation, out var guardsTruePath))
                {
                    return;
                }

                IOperation? guardedPath = null;
                if (searchContext == SearchContext.Indexer)
                {
                    guardedPath = guardsTruePath ? conditionalOperation.WhenTrue : conditionalOperation.WhenFalse;
                }
                else if (searchContext == SearchContext.AddMethod)
                {
                    guardedPath = guardsTruePath ? conditionalOperation.WhenFalse : conditionalOperation.WhenTrue;
                }

                if (guardedPath != null)
                {
                    FindUsages(guardedPath, ref usageContext, searchContext);
                }
                else if (!guardsTruePath && HasReturnOrSetsKeyInTruePath(conditionalOperation, ref usageContext))
                {
                    FindUsageInOperationsAfterConditionBlock(conditionalOperation, ref usageContext, searchContext);
                }

                if (usageContext.UsageLocations.Count == 0)
                {
                    return;
                }

                if (usageContext.SetterLocation != null)
                {
                    usageContext.UsageLocations.Add(usageContext.SetterLocation);
                }

                var diagnostic = containsOperation.CreateDiagnostic(diagnosticDescriptor, usageContext.UsageLocations.ToImmutable(), null);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool AddArgumentIsDeclaredInBlock(IInvocationOperation invocation)
        {
            if (invocation.Parent?.Parent is IBlockOperation block)
            {
                foreach (var operation in block.Operations)
                {
                    var arguments = invocation.Arguments[0].Descendants().Concat(invocation.Arguments[1].Descendants());
                    if (operation is IVariableDeclarationGroupOperation variableGroup)
                    {
                        var declaredVariables = variableGroup.GetDeclaredVariables();
                        if (arguments.Any(d => d is ILocalReferenceOperation local && declaredVariables.Any(v => SymbolEqualityComparer.Default.Equals(v, local.Local))))
                        {
                            return true;
                        }
                    }

                    if (operation is IExpressionStatementOperation { Operation: IAssignmentOperation assignmentOperation }
                        && arguments.Any(d => IsSameReferenceOperation(assignmentOperation.Target, d)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetDictionaryTypeAndMembers(
            Compilation compilation,
            [NotNullWhen(true)] out INamedTypeSymbol? iDictionaryType,
            [NotNullWhen(true)] out IMethodSymbol? containsKeySymbol,
            [NotNullWhen(true)] out IMethodSymbol? addSymbol)
        {
            iDictionaryType = WellKnownTypeProvider.GetOrCreate(compilation).GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIDictionary2);
            if (iDictionaryType is null)
            {
                containsKeySymbol = null;
                addSymbol = null;

                return false;
            }

            containsKeySymbol = iDictionaryType.GetMembers(ContainsKey).OfType<IMethodSymbol>().FirstOrDefault();
            addSymbol = iDictionaryType.GetMembers(Add).OfType<IMethodSymbol>().FirstOrDefault();

            return containsKeySymbol is not null && addSymbol is not null;
        }

        private static bool IsContainsKeyMethod(IMethodSymbol suspectedContainsKeyMethod, IMethodSymbol containsKeyMethod)
        {
            return suspectedContainsKeyMethod.OriginalDefinition.Equals(containsKeyMethod, SymbolEqualityComparer.Default)
                   || DoesSignatureMatch(suspectedContainsKeyMethod, containsKeyMethod);
        }

        private static bool IsDictionaryType([NotNullWhen(true)] ITypeSymbol? suspectedDictionaryType, ISymbol iDictionaryType)
        {
            // Either the type is the IDictionary or it is a type which (indirectly) implements it.
            return suspectedDictionaryType != null
                   && (suspectedDictionaryType.OriginalDefinition.Equals(iDictionaryType, SymbolEqualityComparer.Default)
                       || suspectedDictionaryType.AllInterfaces.Any(static (@interface, dictionary) => @interface.OriginalDefinition.Equals(dictionary, SymbolEqualityComparer.Default),
                           iDictionaryType));
        }

        // Unfortunately we can't do symbol comparison, since this won't work for i.e. a method in a ConcurrentDictionary comparing against the same method in the IDictionary.
        private static bool DoesSignatureMatch(IMethodSymbol suspected, IMethodSymbol comparator)
        {
            return suspected.OriginalDefinition.ReturnType.Name == comparator.ReturnType.Name
                   && suspected.Name == comparator.Name
                   && suspected.Parameters.Length == comparator.Parameters.Length
                   && suspected.Parameters.Zip(comparator.Parameters, (p1, p2) => p1.OriginalDefinition.Type.Name == p2.Type.Name).All(isParameterEqual => isParameterEqual);
        }

        //only handles simple conditions: .. && x.ContainsKey(y) or !x.ContainsKey(y) || ..
        private static bool GetParentConditionalOperation(IOperation operation, ref DictionaryUsageContext usageContext, SearchContext searchContext,
            [NotNullWhen(true)] out IConditionalOperation? conditionalOperation, out bool guardsTruePath)
        {
            guardsTruePath = true;
            if (operation.Parent is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not })
            {
                operation = operation.Parent;
                guardsTruePath = false;
            }

            while (true)
            {
                var parentOperation = operation.Parent;
                IOperation nextOperation;
                switch (parentOperation)
                {
                    case IConditionalOperation c:
                        conditionalOperation = c;

                        return true;
                    case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } bAnd when guardsTruePath:
                        nextOperation = bAnd;
                        if (bAnd.LeftOperand == operation && !FindUsages(bAnd.RightOperand, ref usageContext, searchContext))
                        {
                            conditionalOperation = null;

                            return false;
                        }

                        break;
                    case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalOr } bOr when !guardsTruePath:
                        nextOperation = bOr;
                        if (bOr.LeftOperand == operation && !FindUsages(bOr.RightOperand, ref usageContext, searchContext))
                        {
                            conditionalOperation = null;

                            return false;
                        }

                        break;
                    default:
                        conditionalOperation = null;

                        return false;
                }

                operation = nextOperation;
            }
        }

        private static bool FindUsages(IOperation operation, ref DictionaryUsageContext usageContext, SearchContext searchContext)
        {
            // We don't want to step into multiple layers of conditional statements.
            foreach (var descendant in GetNonConditionalDescendantsAndSelf(operation))
            {
                if (IsSameReferenceOperation(descendant, usageContext.DictionaryReference))
                {
                    switch (descendant.Parent)
                    {
                        case ISimpleAssignmentOperation assign when assign.Target == descendant:
                            return false;
                        case IInvocationOperation invocation when searchContext == SearchContext.Indexer:
                            var methodName = invocation.TargetMethod.Name;
                            switch (methodName)
                            {
                                case Clear:
                                case Add or Remove when invocation.Arguments.Length >= 1 &&
                                                        IsSameConstantOrReferenceOperation(invocation.Arguments[0].Value, usageContext.ContainsKeyArgumentReference):
                                    return false;
                            }

                            break;
                        case IInvocationOperation invocation when searchContext == SearchContext.AddMethod:
                            if (DoesSignatureMatch(invocation.TargetMethod, usageContext.AddSymbol)
                                && IsSameConstantOrReferenceOperation(invocation.Arguments[0].Value, usageContext.ContainsKeyArgumentReference)
                                && invocation.Arguments[1].Value.Kind is OperationKind.Literal or OperationKind.LocalReference or OperationKind.FieldReference or OperationKind.ParameterReference or OperationKind.ConstantPattern
                                && !AddArgumentIsDeclaredInBlock(invocation))
                            {
                                usageContext.UsageLocations.Add(invocation.Syntax.GetLocation());
                            }

                            break;
                        case IPropertyReferenceOperation { Property.IsIndexer: true } indexer
                            when searchContext == SearchContext.Indexer
                                 && IsSameConstantOrReferenceOperation(indexer.Arguments[0].Value, usageContext.ContainsKeyArgumentReference):
                            switch (indexer.Parent)
                            {
                                case ISimpleAssignmentOperation simple when simple.Target == indexer:
                                    FindUsages(simple.Value, ref usageContext, searchContext);

                                    return false;
                                case ICompoundAssignmentOperation compound when compound.Target == indexer:
                                    FindUsages(compound.Value, ref usageContext, searchContext);

                                    return false;
                                case ICoalesceAssignmentOperation coalesce when coalesce.Target == indexer:
                                    return false;
                                case IIncrementOrDecrementOperation inc when inc.Target == indexer &&
                                                                             inc.Parent is not IExpressionStatementOperation:
                                    return false;
                                // C#
                                case IVariableInitializerOperation
                                {
                                    Parent: IVariableDeclaratorOperation
                                    {
                                        Parent: IVariableDeclarationOperation
                                        {
                                            Parent: IVariableDeclarationGroupOperation declarationGroup
                                        } declaration
                                    } declarator
                                } init when init.Value == indexer:
                                    usageContext.UsageLocations.Add(declaration.Children.Count() is 1
                                        ? declarationGroup.Syntax.GetLocation()
                                        : declarator.Syntax.GetLocation());
                                    continue;
                                // VB
                                case IVariableInitializerOperation
                                {
                                    Parent: IVariableDeclarationOperation
                                    {
                                        Parent: IVariableDeclarationGroupOperation declarationGroup
                                    } declaration
                                } init when init.Value == indexer:
                                    usageContext.UsageLocations.Add(declarationGroup.Declarations.Length is 1
                                        ? declarationGroup.Syntax.GetLocation()
                                        : declaration.Syntax.GetLocation());
                                    continue;
                            }

                            usageContext.UsageLocations.Add(indexer.Syntax.GetLocation());

                            break;
                    }
                }
                else
                {
                    switch (descendant.Parent)
                    {
                        case ISimpleAssignmentOperation simple when simple.Target == descendant:
                            if (IsSameReferenceOperation(descendant, usageContext.ContainsKeyArgumentReference) ||
                                IsAnySameReferenceOperation(descendant, usageContext.AdditionalArrayIndexReferences))
                            {
                                FindUsages(simple.Value, ref usageContext, searchContext);

                                return false;
                            }

                            break;
                        case ICompoundAssignmentOperation compound when compound.Target == descendant:
                            if (IsSameReferenceOperation(descendant, usageContext.ContainsKeyArgumentReference) ||
                                IsAnySameReferenceOperation(descendant, usageContext.AdditionalArrayIndexReferences))
                            {
                                FindUsages(compound.Value, ref usageContext, searchContext);

                                return false;
                            }

                            break;
                        case IIncrementOrDecrementOperation increment when increment.Target == descendant:
                            if (IsSameReferenceOperation(descendant, usageContext.ContainsKeyArgumentReference) ||
                                IsAnySameReferenceOperation(descendant, usageContext.AdditionalArrayIndexReferences))
                            {
                                return false;
                            }

                            break;
                    }
                }
            }

            return true;
        }

        private static bool HasReturnOrSetsKeyInTruePath(IConditionalOperation conditionalOperation, ref DictionaryUsageContext usageContext)
        {
            var whenTrue = conditionalOperation.WhenTrue;
            switch (whenTrue)
            {
                case IReturnOperation:
                case IThrowOperation:
                    return true;
                case IBlockOperation block:
                    {
                        foreach (var op in block.Operations)
                        {
                            switch (op)
                            {
                                case IReturnOperation:
                                case IThrowOperation:
                                    return true;
                                case IExpressionStatementOperation expression:
                                    foreach (var childOp in expression.Operation.DescendantsAndSelf())
                                    {
                                        IOperation target;
                                        switch (childOp)
                                        {
                                            case IAssignmentOperation simple:
                                                target = simple.Target;

                                                break;
                                            case IIncrementOrDecrementOperation increment:
                                                target = increment.Target;

                                                break;
                                            default:
                                                continue;
                                        }

                                        if (IsSameReferenceOperation(target, usageContext.ContainsKeyArgumentReference) ||
                                            IsAnySameReferenceOperation(target, usageContext.AdditionalArrayIndexReferences))
                                        {
                                            return false;
                                        }
                                    }

                                    switch (expression.Operation)
                                    {
                                        case IReturnOperation:
                                        case IThrowOperation:
                                            return true;
                                        case ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.IsIndexer: true } reference } when
                                            IsSameReferenceOperation(reference.Instance, usageContext.DictionaryReference) &&
                                            IsSameConstantOrReferenceOperation(reference.Arguments[0].Value, usageContext.ContainsKeyArgumentReference):
                                            {
                                                usageContext.SetterLocation = expression.Syntax.GetLocation();

                                                continue;
                                            }
                                        case IInvocationOperation { TargetMethod.Name: Add } invocation when
                                            IsSameReferenceOperation(invocation.Instance, usageContext.DictionaryReference) &&
                                            IsSameConstantOrReferenceOperation(invocation.Arguments[0].Value, usageContext.ContainsKeyArgumentReference):
                                            {
                                                usageContext.SetterLocation = expression.Syntax.GetLocation();

                                                continue;
                                            }
                                    }

                                    break;
                            }
                        }

                        break;
                    }
            }

            return usageContext.SetterLocation != null;
        }

        private static void FindUsageInOperationsAfterConditionBlock(IOperation sourceOperation, ref DictionaryUsageContext context, SearchContext searchContext)
        {
            var testOperation = false;
            foreach (var operation in sourceOperation.Parent!.Children)
            {
                if (!testOperation)
                {
                    testOperation = operation == sourceOperation;

                    continue;
                }

                if (!FindUsages(operation, ref context, searchContext))
                {
                    break;
                }
            }
        }

        private static bool IsSameConstantOrReferenceOperation(IOperation sourceReference, IOperation targetReference)
        {
            if (targetReference.ConstantValue.HasValue && sourceReference.ConstantValue.HasValue)
            {
                return sourceReference.ConstantValue.Equals(targetReference.ConstantValue);
            }

            return IsSameReferenceOperation(sourceReference, targetReference);
        }

        private static bool IsSameReferenceOperation(IOperation? sourceReference, IOperation targetReference)
        {
            switch (sourceReference)
            {
                case ILocalReferenceOperation source when targetReference is ILocalReferenceOperation target:
                    return target.Local.Equals(source.Local, SymbolEqualityComparer.Default);
                case IParameterReferenceOperation source when targetReference is IParameterReferenceOperation target:
                    return target.Parameter.Equals(source.Parameter, SymbolEqualityComparer.Default);
                case IFieldReferenceOperation source when targetReference is IFieldReferenceOperation target:
                    return target.Field.Equals(source.Field, SymbolEqualityComparer.Default) && AreInstancesEqual(source, target);
                case IPropertyReferenceOperation source when targetReference is IPropertyReferenceOperation target:
                    return target.Property.Equals(source.Property, SymbolEqualityComparer.Default) && AreInstancesEqual(source, target);
                case IMemberReferenceOperation source when targetReference is IMemberReferenceOperation target:
                    return target.Member.Equals(source.Member, SymbolEqualityComparer.Default);
                case IArrayElementReferenceOperation source when targetReference is IArrayElementReferenceOperation target:
                    if (source.Indices.Length != target.Indices.Length || !IsSameReferenceOperation(source.ArrayReference, target.ArrayReference))
                    {
                        return false;
                    }

                    for (int i = 0; i < target.Indices.Length; i++)
                    {
                        if (!IsSameConstantOrReferenceOperation(source.Indices[i], target.Indices[i]))
                            return false;
                    }

                    return true;
            }

            return false;
        }

        private static bool IsAnySameReferenceOperation(IOperation source, ImmutableArray<IOperation> targets)
        {
            foreach (var target in targets)
            {
                if (IsSameReferenceOperation(source, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<IOperation> GetNonConditionalDescendantsAndSelf(IOperation operation)
        {
            var childOperations = operation.Children.SelectMany(c =>
            {
                if (c is not IConditionalOperation)
                {
                    return GetNonConditionalDescendantsAndSelf(c);
                }

                return Enumerable.Empty<IOperation>();
            });

            return[operation, .. childOperations];
        }

        private static bool AreInstancesEqual(IOperation instance1, IOperation instance2)
        {
            string syntax1 = instance1.Syntax
                .ToString()
                .Replace("this.", string.Empty)
                .Replace("Me.", string.Empty);
            string syntax2 = instance2.Syntax
                .ToString()
                .Replace("this.", string.Empty)
                .Replace("Me.", string.Empty);

            return syntax1 == syntax2;
        }

        private enum SearchContext
        {
            None = 0,
            Indexer,
            AddMethod
        }
    }
}