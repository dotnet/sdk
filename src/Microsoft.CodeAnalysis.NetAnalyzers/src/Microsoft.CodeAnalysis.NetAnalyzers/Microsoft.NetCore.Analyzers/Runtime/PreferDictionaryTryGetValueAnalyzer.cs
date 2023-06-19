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

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1854: <inheritdoc cref="PreferDictionaryTryGetValueTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferDictionaryTryGetValueAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1854";

        private const string ContainsKeyMethodeName = nameof(IDictionary<dynamic, dynamic>.ContainsKey);
        internal const string AddMethodeName = nameof(IDictionary<dynamic, dynamic>.Add);
        private const string RemoveMethodeName = nameof(IDictionary<dynamic, dynamic>.Remove);
        private const string ClearMethodName = nameof(IDictionary<dynamic, dynamic>.Clear);

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueTitle));
        private static readonly LocalizableString s_localizableTryGetValueMessage = CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueMessage));
        private static readonly LocalizableString s_localizableTryGetValueDescription = CreateLocalizableResourceString(nameof(PreferDictionaryTryGetValueDescription));

        internal static readonly DiagnosticDescriptor ContainsKeyRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableTryGetValueMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableTryGetValueDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ContainsKeyRule);

        private struct DictionaryUsageContext : System.IEquatable<DictionaryUsageContext>
        {
            public DictionaryUsageContext(IOperation dictionaryReference, IOperation indexReference)
            {
                DictionaryReference = dictionaryReference;
                IndexReference = indexReference;

                while (dictionaryReference is IArrayElementReferenceOperation a)
                {
                    AdditionalArrayIndexReferences = AdditionalArrayIndexReferences.AddRange(a.Indices);
                    dictionaryReference = a.ArrayReference;
                }
            }

            public bool Equals(DictionaryUsageContext other)
            {
                return Equals(_usageLocations, other._usageLocations) &&
                       DictionaryReference.Equals(other.DictionaryReference) &&
                       IndexReference.Equals(other.IndexReference) &&
                       Equals(SetterLocation, other.SetterLocation) &&
                       Equals(AdditionalArrayIndexReferences, other.AdditionalArrayIndexReferences);
            }

            public override bool Equals(object? obj)
            {
                return obj is DictionaryUsageContext other && Equals(other);
            }

            public override int GetHashCode()
            {
                return RoslynHashCode.Combine(_usageLocations, DictionaryReference, IndexReference, SetterLocation, AdditionalArrayIndexReferences);
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
            public IOperation IndexReference { get; }

            public ImmutableArray<IOperation> AdditionalArrayIndexReferences { get; } = ImmutableArray<IOperation>.Empty;

            public ImmutableArray<Location>.Builder UsageLocations
            {
                get
                {
                    _usageLocations ??= ImmutableArray.CreateBuilder<Location>();
                    return _usageLocations;
                }
            }

            public Location? SetterLocation
            {
                get;
                internal set;
            }

            private ImmutableArray<Location>.Builder? _usageLocations;
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var compilation = compilationContext.Compilation;
            if (!TryGetDictionaryTypeAndMembers(compilation, out var iDictionaryType, out var containsKeySymbol))
            {
                return;
            }

            compilationContext.RegisterOperationAction(context =>
                OnOperationAction(context, iDictionaryType, containsKeySymbol), OperationKind.Invocation);
        }

        private static void OnOperationAction(OperationAnalysisContext context, INamedTypeSymbol dictionaryType,
            IMethodSymbol containsKeySymbol)
        {
            var containsOperation = (IInvocationOperation)context.Operation;
            if (!IsContainsKeyMethod(containsOperation.TargetMethod, containsKeySymbol) ||
                !IsDictionaryType(containsOperation.GetReceiverType(context.Compilation, true,
                    context.CancellationToken), dictionaryType))
            {
                return;
            }

            var usageContext = new DictionaryUsageContext(containsOperation.Instance, containsOperation.Arguments[0].Value);
            if (!GetParentConditionalOperation(containsOperation, ref usageContext, out var conditionalOperation, out var guardsTruePath))
                return;

            var guardedPath = guardsTruePath ? conditionalOperation.WhenTrue : conditionalOperation.WhenFalse;
            if (guardedPath != null)
                FindUsages(guardedPath, ref usageContext);
            else if (!guardsTruePath && HasReturnOrSetsKeyInTruePath(conditionalOperation, ref usageContext))
                FindUsageInOperationsAfterConditionBlock(conditionalOperation, ref usageContext);

            if (usageContext.UsageLocations.Count == 0)
                return;

            if (usageContext.SetterLocation != null)
                usageContext.UsageLocations.Add(usageContext.SetterLocation);

            context.ReportDiagnostic(Diagnostic.Create(ContainsKeyRule, containsOperation.Syntax.GetLocation(),
                usageContext.UsageLocations.ToImmutable()));
        }

        //only handles simple conditions: .. && x.ContainsKey(y) or !x.ContainsKey(y) || ..
        private static bool GetParentConditionalOperation(IOperation operation, ref DictionaryUsageContext context, [NotNullWhen(true)] out IConditionalOperation? conditionalOperation, out bool guardsTruePath)
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
                        if (bAnd.LeftOperand == operation && !FindUsages(bAnd.RightOperand, ref context))
                        {
                            conditionalOperation = null;
                            return false;
                        }

                        break;
                    case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalOr } bOr when !guardsTruePath:
                        nextOperation = bOr;
                        if (bOr.LeftOperand == operation && !FindUsages(bOr.RightOperand, ref context))
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

        private static bool FindUsages(IOperation operation, ref DictionaryUsageContext context)
        {
            foreach (var descendant in operation.DescendantsAndSelf())
            {
                if (IsSameReferenceOperation(descendant, context.DictionaryReference))
                {
                    switch (descendant.Parent)
                    {
                        case ISimpleAssignmentOperation assign when assign.Target == descendant:
                            return false;
                        case IInvocationOperation invocation:
                            var methodName = invocation.TargetMethod.Name;
                            switch (methodName)
                            {
                                case ClearMethodName:
                                case AddMethodeName or RemoveMethodeName when invocation.Arguments.Length >= 1 &&
                                      IsSameConstantOrReferenceOperation(invocation.Arguments[0].Value, context.IndexReference):
                                    return false;
                            }

                            break;
                        case IPropertyReferenceOperation { Property.IsIndexer: true } indexer
                            when IsSameConstantOrReferenceOperation(indexer.Arguments[0].Value, context.IndexReference):
                            switch (indexer.Parent)
                            {
                                case ISimpleAssignmentOperation simple when simple.Target == indexer:
                                    FindUsages(simple.Value, ref context);
                                    return false;
                                case ICompoundAssignmentOperation compound when compound.Target == indexer:
                                    FindUsages(compound.Value, ref context);
                                    return false;
                                case ICoalesceAssignmentOperation coalesce when coalesce.Target == indexer:
                                    return false;
                                case IIncrementOrDecrementOperation inc when inc.Target == indexer &&
                                    inc.Parent is not IExpressionStatementOperation:
                                    return false;
                            }

                            context.UsageLocations.Add(indexer.Syntax.GetLocation());
                            break;
                    }
                }
                else
                {
                    switch (descendant.Parent)
                    {
                        case ISimpleAssignmentOperation simple when simple.Target == descendant:
                            if (IsSameReferenceOperation(descendant, context.IndexReference) ||
                                IsAnySameReferenceOperation(descendant, context.AdditionalArrayIndexReferences))
                            {
                                FindUsages(simple.Value, ref context);
                                return false;
                            }

                            break;
                        case ICompoundAssignmentOperation compound when compound.Target == descendant:
                            if (IsSameReferenceOperation(descendant, context.IndexReference) ||
                                IsAnySameReferenceOperation(descendant, context.AdditionalArrayIndexReferences))
                            {
                                FindUsages(compound.Value, ref context);
                                return false;
                            }

                            break;
                        case IIncrementOrDecrementOperation increment when increment.Target == descendant:
                            if (IsSameReferenceOperation(descendant, context.IndexReference) ||
                                IsAnySameReferenceOperation(descendant, context.AdditionalArrayIndexReferences))
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
                                            case ISimpleAssignmentOperation simple:
                                                target = simple.Target;
                                                break;
                                            case ICompoundAssignmentOperation compound:
                                                target = compound.Target;
                                                break;
                                            case ICoalesceAssignmentOperation coalesce:
                                                target = coalesce.Target;
                                                break;
                                            case IIncrementOrDecrementOperation increment:
                                                target = increment.Target;
                                                break;
                                            default:
                                                continue;
                                        }

                                        if (IsSameReferenceOperation(target, usageContext.IndexReference) ||
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
                                            IsSameConstantOrReferenceOperation(reference.Arguments[0].Value, usageContext.IndexReference):
                                            {
                                                usageContext.SetterLocation = expression.Syntax.GetLocation();
                                                continue;
                                            }
                                        case IInvocationOperation { TargetMethod.Name: AddMethodeName } invocation when
                                            IsSameReferenceOperation(invocation.Instance, usageContext.DictionaryReference) &&
                                            IsSameConstantOrReferenceOperation(invocation.Arguments[0].Value, usageContext.IndexReference):
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

        private static void FindUsageInOperationsAfterConditionBlock(IOperation sourceOperation, ref DictionaryUsageContext context)
        {
            var testOperation = false;
            foreach (var operation in sourceOperation.Parent.Children)
            {
                if (!testOperation)
                {
                    testOperation = operation == sourceOperation;
                    continue;
                }

                if (!FindUsages(operation, ref context))
                    break;
            }
        }

        private static bool IsSameConstantOrReferenceOperation(IOperation sourceReference, IOperation targetReference)
        {
            if (targetReference.ConstantValue.HasValue && sourceReference.ConstantValue.HasValue)
                return sourceReference.ConstantValue.Equals(targetReference.ConstantValue);
            return IsSameReferenceOperation(sourceReference, targetReference);
        }

        private static bool IsSameReferenceOperation(IOperation sourceReference, IOperation targetReference)
        {
            switch (sourceReference)
            {
                case ILocalReferenceOperation source when targetReference is ILocalReferenceOperation target:
                    return target.Local.Equals(source.Local, SymbolEqualityComparer.Default);
                case IParameterReferenceOperation source when targetReference is IParameterReferenceOperation target:
                    return target.Parameter.Equals(source.Parameter, SymbolEqualityComparer.Default);
                case IFieldReferenceOperation source when targetReference is IFieldReferenceOperation target:
                    return target.Field.Equals(source.Field, SymbolEqualityComparer.Default);
                case IPropertyReferenceOperation source when targetReference is IPropertyReferenceOperation target:
                    return target.Property.Equals(source.Property, SymbolEqualityComparer.Default);
                case IMemberReferenceOperation source when targetReference is IMemberReferenceOperation target:
                    return target.Member.Equals(source.Member, SymbolEqualityComparer.Default);
                case IArrayElementReferenceOperation source when targetReference is IArrayElementReferenceOperation target:
                    if (source.Indices.Length != target.Indices.Length || !IsSameReferenceOperation(source.ArrayReference, target.ArrayReference))
                        return false;

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
                    return true;
            }

            return false;
        }

        private static bool TryGetDictionaryTypeAndMembers(Compilation compilation,
            [NotNullWhen(true)] out INamedTypeSymbol? iDictionaryType,
            [NotNullWhen(true)] out IMethodSymbol? containsKeySymbol)
        {
            containsKeySymbol = null;
            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIDictionary2, out iDictionaryType))
            {
                return false;
            }

            containsKeySymbol = iDictionaryType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == ContainsKeyMethodeName);

            return containsKeySymbol is not null;
        }

        private static bool IsContainsKeyMethod(IMethodSymbol suspectedContainsKeyMethod, IMethodSymbol containsKeyMethod)
        {
            return suspectedContainsKeyMethod.OriginalDefinition.Equals(containsKeyMethod, SymbolEqualityComparer.Default)
                   || DoesSignatureMatch(suspectedContainsKeyMethod, containsKeyMethod);
        }

        private static bool IsDictionaryType(ITypeSymbol? suspectedDictionaryType, ISymbol iDictionaryType)
        {
            // Either the type is the IDictionary or it is a type which (indirectly) implements it.
            return suspectedDictionaryType != null && (suspectedDictionaryType.OriginalDefinition.Equals(iDictionaryType, SymbolEqualityComparer.Default)
                   || suspectedDictionaryType.AllInterfaces.Any((@interface, dictionary) => @interface.OriginalDefinition.Equals(dictionary, SymbolEqualityComparer.Default), iDictionaryType));
        }

        // Unfortunately we can't do symbol comparison, since this won't work for i.e. a method in a ConcurrentDictionary comparing against the same method in the IDictionary.
        private static bool DoesSignatureMatch(IMethodSymbol suspected, IMethodSymbol comparator)
        {
            return suspected.OriginalDefinition.ReturnType.Name == comparator.ReturnType.Name
                   && suspected.Name == comparator.Name
                   && suspected.Parameters.Length == comparator.Parameters.Length
                   && suspected.Parameters.Zip(comparator.Parameters, (p1, p2) => p1.OriginalDefinition.Type.Name == p2.Type.Name).All(isParameterEqual => isParameterEqual);
        }
    }
}