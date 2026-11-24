// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;
using static Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerationsHelpers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    internal abstract partial class AvoidMultipleEnumerations
    {
        private static bool CanSkipAnalysis(
            OperationBlockAnalysisContext context,
            PooledHashSet<IOperation> potentialDiagnosticOperations,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (context.OwningSymbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
            {
                return false;
            }

            using var enumeratedSymbols = PooledHashSet<ISymbol>.GetInstance(SymbolEqualityComparer.Default);

            foreach (var operation in potentialDiagnosticOperations)
            {
                var symbol = GetReferencedSymbol(operation);
                if (symbol == null
                    || !enumeratedSymbols.Add(symbol)
                    || operation.IsWithinLambdaOrLocalFunction(out _)
                    || IsRepeatedInLoop(operation))
                {
                    return false;
                }

                var (tail, count) = SkipLinqChainAndConversionMethod(operation, wellKnownSymbolsInfo);
                if (count == EnumerationCount.TwoOrMoreTime
                    || (count == EnumerationCount.One
                        && (IsOperationEnumeratedByInvocation(tail, wellKnownSymbolsInfo)
                            || IsOperationEnumeratedByForEachLoop(tail, wellKnownSymbolsInfo))))
                {
                    return false;
                }
            }

            if (context.Options.GetInterproceduralAnalysisKindOption(
                MultipleEnumerableDescriptor,
                context.OperationBlocks[0].Syntax.SyntaxTree,
                context.Compilation,
                InterproceduralAnalysisKind.None) != InterproceduralAnalysisKind.None)
            {
                return false;
            }

            using var aliases = PooledDictionary<ISymbol, (ISymbol Parent, int Rank, bool MultipleInputs)>.GetInstance(SymbolEqualityComparer.Default);
            var walker = new EnumerationSourceWalker(aliases, wellKnownSymbolsInfo);

            foreach (var operationBlock in context.OperationBlocks)
            {
                walker.Visit(operationBlock);
            }

            if (!walker.IsSupported)
            {
                return false;
            }

            // Merge all possible assignments, regardless of branch or ordering. A
            // deferred expression with multiple inputs can enumerate a shared source
            // twice, so leave those groups to the flow analysis.
            using var enumeratedSources = PooledHashSet<ISymbol>.GetInstance(SymbolEqualityComparer.Default);

            foreach (var symbol in enumeratedSymbols)
            {
                if (walker.HasMultipleInputs(symbol)
                    || !enumeratedSources.Add(walker.FindSource(symbol)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRepeatedInLoop(IOperation operation)
        {
            var child = operation;

            for (var parent = operation.Parent; parent != null; child = parent, parent = parent.Parent)
            {
                if (parent is ILoopOperation
                    && (parent is not IForEachLoopOperation forEach || child != forEach.Collection))
                {
                    return true;
                }
            }

            return false;
        }

        private static ISymbol? GetReferencedSymbol(IOperation operation)
            => operation switch
            {
                ILocalReferenceOperation local => local.Local,
                IParameterReferenceOperation parameter => parameter.Parameter,
                IFieldReferenceOperation field => field.Field,
                IPropertyReferenceOperation property => property.Property,
                _ => null
            };

        private sealed class EnumerationSourceWalker : OperationWalker
        {
            private readonly PooledDictionary<ISymbol, (ISymbol Parent, int Rank, bool MultipleInputs)> _aliases;
            private readonly WellKnownSymbolsInfo _wellKnownSymbolsInfo;
            private ISymbol? _assignmentTarget;
            private int _assignmentReferenceCount;

            public bool IsSupported { get; private set; } = true;

            public EnumerationSourceWalker(
                PooledDictionary<ISymbol, (ISymbol Parent, int Rank, bool MultipleInputs)> aliases,
                WellKnownSymbolsInfo wellKnownSymbolsInfo)
            {
                _aliases = aliases;
                _wellKnownSymbolsInfo = wellKnownSymbolsInfo;
            }

            private bool CanHoldEnumerable(ITypeSymbol? type)
                => type != null && (type.IsReferenceType || type.TypeKind == TypeKind.TypeParameter
                    || type is INamedTypeSymbol { TypeKind: TypeKind.Struct, SpecialType: SpecialType.None }
                    || IsDeferredType(type.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes));

            private bool IsEnumerableBindingPattern(IOperation operation)
                => operation switch
                {
                    IDeclarationPatternOperation { DeclaredSymbol: { } symbol } => CanHoldEnumerable(symbol.GetMemberOrLocalOrParameterType()),
                    IRecursivePatternOperation { DeclaredSymbol: { } symbol } => CanHoldEnumerable(symbol.GetMemberOrLocalOrParameterType()),
                    IListPatternOperation { DeclaredSymbol: { } symbol } => CanHoldEnumerable(symbol.GetMemberOrLocalOrParameterType()),
                    _ => false
                };

            public override void Visit(IOperation? operation)
            {
                if (!IsSupported || operation == null)
                {
                    return;
                }

                if (operation is IBranchOperation { BranchKind: BranchKind.GoTo }
                    || operation is IDeconstructionAssignmentOperation
                    || IsEnumerableBindingPattern(operation)
                    || operation is IArgumentOperation { Parameter.RefKind: not RefKind.None } argument && CanHoldEnumerable(argument.Value.Type)
                    || operation is ILocalReferenceOperation { Local.RefKind: not RefKind.None } local && CanHoldEnumerable(local.Type)
                    || operation is IParameterReferenceOperation { Parameter.RefKind: not RefKind.None } parameter && CanHoldEnumerable(parameter.Type))
                {
                    IsSupported = false;
                    return;
                }

                if (_assignmentTarget != null && CanHoldEnumerable(operation.Type)
                    && GetReferencedSymbol(operation) is { } source)
                {
                    Union(_assignmentTarget, source);
                    if (++_assignmentReferenceCount > 1)
                    {
                        var root = FindSource(_assignmentTarget);
                        var entry = _aliases[root];
                        _aliases[root] = (root, entry.Rank, true);
                    }
                }

                base.Visit(operation);
            }

            public override void VisitVariableDeclarator(IVariableDeclaratorOperation operation)
            {
                var savedTarget = _assignmentTarget;
                var savedCount = _assignmentReferenceCount;
                _assignmentTarget = CanHoldEnumerable(operation.Symbol.Type) ? operation.Symbol : null;
                _assignmentReferenceCount = 0;
                base.VisitVariableDeclarator(operation);
                _assignmentTarget = savedTarget;
                _assignmentReferenceCount = savedCount;
            }

            public override void VisitVariableDeclaration(IVariableDeclarationOperation operation)
            {
                foreach (var declarator in operation.Declarators)
                {
                    Visit(declarator);
                }

                if (operation.Initializer != null)
                {
                    var savedTarget = _assignmentTarget;
                    var savedCount = _assignmentReferenceCount;
                    _assignmentTarget = null;
                    _assignmentReferenceCount = 0;

                    foreach (var declarator in operation.Declarators)
                    {
                        if (CanHoldEnumerable(declarator.Symbol.Type))
                        {
                            if (_assignmentTarget != null)
                            {
                                Union(_assignmentTarget, declarator.Symbol);
                            }

                            _assignmentTarget = declarator.Symbol;
                        }
                    }

                    Visit(operation.Initializer);
                    _assignmentTarget = savedTarget;
                    _assignmentReferenceCount = savedCount;
                }
            }

            public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
            {
                if (operation.IsRef)
                {
                    IsSupported = false;
                    return;
                }

                VisitAssignment(operation.Target, operation.Value);
            }

            public override void VisitCoalesceAssignment(ICoalesceAssignmentOperation operation)
                => VisitAssignment(operation.Target, operation.Value);

            public override void VisitForEachLoop(IForEachLoopOperation operation)
            {
                var control = operation.LoopControlVariable is IVariableDeclaratorOperation declarator
                    ? declarator.Symbol
                    : GetReferencedSymbol(operation.LoopControlVariable);
                if (control != null && CanHoldEnumerable(control.GetMemberOrLocalOrParameterType()))
                {
                    var savedTarget = _assignmentTarget;
                    var savedCount = _assignmentReferenceCount;
                    _assignmentTarget = control;
                    _assignmentReferenceCount = 0;
                    Visit(operation.Collection);
                    _assignmentTarget = savedTarget;
                    _assignmentReferenceCount = savedCount;

                    foreach (var child in operation.ChildOperations)
                    {
                        if (child != operation.Collection)
                        {
                            Visit(child);
                        }
                    }
                }
                else
                {
                    base.VisitForEachLoop(operation);
                }
            }

            public override void VisitCompoundAssignment(ICompoundAssignmentOperation operation)
            {
                if (CanHoldEnumerable(operation.Target.Type))
                {
                    IsSupported = false;
                    return;
                }

                base.VisitCompoundAssignment(operation);
            }

            private void VisitAssignment(IOperation target, IOperation value)
            {
                Visit(target);
                var savedTarget = _assignmentTarget;
                var savedCount = _assignmentReferenceCount;
                _assignmentTarget = CanHoldEnumerable(target.Type) ? GetReferencedSymbol(target) : null;
                _assignmentReferenceCount = 0;
                if (CanHoldEnumerable(target.Type) && _assignmentTarget == null)
                {
                    IsSupported = false;
                }
                else
                {
                    Visit(value);
                }

                _assignmentTarget = savedTarget;
                _assignmentReferenceCount = savedCount;
            }

            public override void VisitIsPattern(IIsPatternOperation operation)
            {
                if (operation.Pattern is IDeclarationPatternOperation { DeclaredSymbol: { } declared }
                    && CanHoldEnumerable(operation.Value.Type))
                {
                    var savedTarget = _assignmentTarget;
                    var savedCount = _assignmentReferenceCount;
                    _assignmentTarget = declared;
                    _assignmentReferenceCount = 0;
                    Visit(operation.Value);
                    _assignmentTarget = savedTarget;
                    _assignmentReferenceCount = savedCount;
                }
                else if (operation.Pattern.HasAnyOperationDescendant(IsEnumerableBindingPattern))
                {
                    IsSupported = false;
                }
                else
                {
                    base.VisitIsPattern(operation);
                }
            }

            public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
                => CheckNestedFunction(operation.Body, operation.Symbol);

            public override void VisitLocalFunction(ILocalFunctionOperation operation)
                => CheckNestedFunction(operation.Body, operation.Symbol);

            private void CheckNestedFunction(IOperation? body, IMethodSymbol symbol)
            {
                if (body != null && body.HasAnyOperationDescendant(operation =>
                    IsDeferredType(operation.Type?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes)
                    || CanHoldEnumerable(operation.Type) && GetReferencedSymbol(operation) is { } referenced
                        && !SymbolEqualityComparer.Default.Equals(referenced.ContainingSymbol, symbol)))
                {
                    IsSupported = false;
                }
            }

            public ISymbol FindSource(ISymbol symbol)
            {
                if (!_aliases.TryGetValue(symbol, out var entry))
                {
                    _aliases.Add(symbol, (symbol, 0, false));
                    return symbol;
                }

                if (SymbolEqualityComparer.Default.Equals(symbol, entry.Parent))
                {
                    return symbol;
                }

                var root = FindSource(entry.Parent);
                _aliases[symbol] = (root, entry.Rank, entry.MultipleInputs);
                return root;
            }

            public bool HasMultipleInputs(ISymbol symbol)
                => _aliases[FindSource(symbol)].MultipleInputs;

            private void Union(ISymbol first, ISymbol second)
            {
                first = FindSource(first);
                second = FindSource(second);
                if (SymbolEqualityComparer.Default.Equals(first, second))
                {
                    return;
                }

                var firstEntry = _aliases[first];
                var secondEntry = _aliases[second];
                var firstRank = firstEntry.Rank;
                var secondRank = secondEntry.Rank;
                var multipleInputs = firstEntry.MultipleInputs || secondEntry.MultipleInputs;
                if (firstRank < secondRank)
                {
                    _aliases[first] = (second, firstRank, firstEntry.MultipleInputs);
                    _aliases[second] = (second, secondRank, multipleInputs);
                }
                else
                {
                    _aliases[second] = (first, secondRank, secondEntry.MultipleInputs);
                    _aliases[first] = (first, firstRank == secondRank ? firstRank + 1 : firstRank, multipleInputs);
                }
            }
        }
    }
}
