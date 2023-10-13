// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.Operations;
using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidSingleUseOfLocalJsonSerializerOptions : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor s_Rule = DiagnosticDescriptorHelper.Create(
            id: "CA1869",
            title: CreateLocalizableResourceString(nameof(AvoidSingleUseOfLocalJsonSerializerOptionsTitle)),
            messageFormat: CreateLocalizableResourceString(nameof(AvoidSingleUseOfLocalJsonSerializerOptionsMessage)),
            category: DiagnosticCategory.Performance,
            ruleLevel: RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(AvoidSingleUseOfLocalJsonSerializerOptionsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            Compilation compilation = context.Compilation;

            compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemTextJsonJsonSerializerOptions, out INamedTypeSymbol? jsonSerializerOptionsSymbol);

            compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemTextJsonJsonSerializer, out INamedTypeSymbol? jsonSerializerSymbol);

            if (jsonSerializerOptionsSymbol is null || jsonSerializerSymbol is null)
            {
                return;
            }

            context.RegisterOperationAction(
                context =>
                {
                    var operation = (IObjectCreationOperation)context.Operation;

                    INamedTypeSymbol? typeSymbol = operation.Constructor?.ContainingType;
                    if (SymbolEqualityComparer.Default.Equals(typeSymbol, jsonSerializerOptionsSymbol))
                    {
                        if (IsCtorUsedAsArgumentForJsonSerializer(operation, jsonSerializerSymbol) ||
                            IsLocalUsedAsArgumentForJsonSerializerOnly(operation, jsonSerializerSymbol, jsonSerializerOptionsSymbol))
                        {
                            context.ReportDiagnostic(operation.CreateDiagnostic(s_Rule));
                        }
                    }
                },
                OperationKind.ObjectCreation);
        }

        private static bool IsCtorUsedAsArgumentForJsonSerializer(IObjectCreationOperation objCreationOperation, INamedTypeSymbol jsonSerializerSymbol)
        {
            IOperation operation = WalkUpConditional(objCreationOperation);

            return operation.Parent is IArgumentOperation argumentOperation &&
                IsArgumentForJsonSerializer(argumentOperation, jsonSerializerSymbol);
        }

        private static bool IsArgumentForJsonSerializer(IArgumentOperation argumentOperation, INamedTypeSymbol jsonSerializerSymbol)
        {
            return argumentOperation.Parent is IInvocationOperation invocationOperation &&
                SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod.ContainingType, jsonSerializerSymbol);
        }

        private static bool IsLocalUsedAsArgumentForJsonSerializerOnly(IObjectCreationOperation objCreation, INamedTypeSymbol jsonSerializerSymbol, INamedTypeSymbol jsonSerializerOptionsSymbol)
        {
            IOperation operation = WalkUpConditional(objCreation);
            if (!IsLocalAssignment(operation, jsonSerializerOptionsSymbol, out List<ILocalSymbol>? localSymbols))
            {
                return false;
            }

            IBlockOperation? localBlock = objCreation.GetFirstParentBlock();
            bool isSingleUseJsonSerializerInvocation = false;

            foreach (IOperation descendant in localBlock.Descendants())
            {
                if (descendant is not ILocalReferenceOperation localRefOperation ||
                    !localSymbols.Contains(localRefOperation.Local))
                {
                    continue;
                }

                // Symbol is declared in a parent scope and referenced inside a loop,
                // this implies that options are used more than once.
                if (IsLocalReferenceInsideChildLoop(localRefOperation, localBlock!))
                {
                    return false;
                }

                // Avoid cases that would potentially make the local escape current block scope.
                if (IsArgumentOfJsonSerializer(descendant, jsonSerializerSymbol, out bool isArgumentOfInvocation))
                {
                    // Case: used more than once i.e: not single-use.
                    if (isSingleUseJsonSerializerInvocation)
                    {
                        return false;
                    }

                    isSingleUseJsonSerializerInvocation = true;
                }

                // Case: passed as argument of a non-JsonSerializer method.
                else if (isArgumentOfInvocation)
                {
                    return false;
                }

                if (IsFieldOrPropertyAssignment(descendant))
                {
                    return false;
                }

                // Case: deconstruction assignment.
                if (IsTupleForDeconstructionTargetingFieldOrProperty(descendant))
                {
                    return false;
                }

                // Case: local goes into closure.
                if (IsClosureOnLambdaOrLocalFunction(descendant, localBlock!))
                {
                    return false;
                }
            }

            return isSingleUseJsonSerializerInvocation;
        }

        [return: NotNullIfNotNull(nameof(operation))]
        private static IOperation? WalkUpConditional(IOperation? operation)
        {
            if (operation is null)
                return null;

            while (operation.Parent is IConditionalOperation conditionalOperation)
            {
                operation = conditionalOperation;
            }

            return operation;
        }

        private static bool IsLocalReferenceInsideChildLoop(ILocalReferenceOperation localRef, IBlockOperation symbolBlock)
        {
            IOperation? current = localRef;
            while ((current = current?.Parent) is not null)
            {
                if (current is ILoopOperation loop)
                {
                    Debug.Assert(loop.Body is IBlockOperation);
                    return loop.Body != symbolBlock;
                }

                if (current == symbolBlock)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsArgumentOfJsonSerializer(IOperation operation, INamedTypeSymbol jsonSerializerSymbol, out bool isArgumentOfInvocation)
        {
            if (operation.Parent is IArgumentOperation arg && arg.Parent is IInvocationOperation inv)
            {
                isArgumentOfInvocation = true;
                return SymbolEqualityComparer.Default.Equals(inv.TargetMethod.ContainingType, jsonSerializerSymbol);
            }

            isArgumentOfInvocation = false;
            return false;
        }

        private static bool IsFieldOrPropertyAssignment(IOperation operation)
        {
            IOperation? current = operation.Parent;

            while (current is IAssignmentOperation assignment)
            {
                if (assignment.Target is IFieldReferenceOperation or IPropertyReferenceOperation)
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static bool IsTupleForDeconstructionTargetingFieldOrProperty(IOperation operation)
        {
            IOperation? current = operation.Parent;

            if (current is not ITupleOperation tuple)
            {
                return false;
            }

            Stack<int> depth = new Stack<int>();
            depth.Push(tuple.Elements.IndexOf(operation));

            // walk-up right-hand nested tuples.
            while (tuple.Parent is ITupleOperation parent)
            {
                depth.Push(parent.Elements.IndexOf(tuple));
                tuple = parent;
            }

            current = tuple.WalkUpConversion().Parent;
            if (current is not IDeconstructionAssignmentOperation deconstruction)
            {
                return false;
            }

            // walk-down left-hand nested tuples and see if it targets a field or property.
            if (deconstruction.Target is not ITupleOperation deconstructionTarget)
            {
                return false;
            }

            tuple = deconstructionTarget;

            IOperation? target = null;
            while (depth.Count > 0)
            {
                int idx = depth.Pop();
                target = tuple.Elements[idx];

                if (target is ITupleOperation targetAsTuple)
                {
                    tuple = targetAsTuple;
                }
                else if (depth.Count > 0)
                {
                    return false;
                }
            }

            return target is IFieldReferenceOperation or IPropertyReferenceOperation;
        }

        private static bool IsClosureOnLambdaOrLocalFunction(IOperation operation, IBlockOperation localBlock)
        {
            if (!operation.IsWithinLambdaOrLocalFunction(out IOperation? lambdaOrLocalFunc))
            {
                return false;
            }

            IBlockOperation? block = lambdaOrLocalFunc switch
            {
                IAnonymousFunctionOperation lambda => lambda.Body,
                ILocalFunctionOperation localFunc => localFunc.Body,
                _ => throw new InvalidOperationException()
            };

            return block != localBlock;
        }

        private static bool IsLocalAssignment(IOperation operation, INamedTypeSymbol jsonSerializerOptionsSymbol, [NotNullWhen(true)] out List<ILocalSymbol>? localSymbols)
        {
            localSymbols = null;
            IOperation? currentOperation = operation.Parent;

            while (currentOperation is not null)
            {
                // ignore cases where the object creation or one of its parents is used as argument.
                if (currentOperation is IArgumentOperation)
                {
                    return false;
                }

                // for cases like:
                // var options;
                // options = new JsonSerializerOptions();
                if (currentOperation is IExpressionStatementOperation)
                {
                    IOperation? tmpOperation = operation.Parent;
                    while (tmpOperation is IAssignmentOperation assignment)
                    {
                        if (assignment.Target is IFieldReferenceOperation or IPropertyReferenceOperation)
                        {
                            return false;
                        }
                        else if (assignment.Target is ILocalReferenceOperation localRef &&
                            SymbolEqualityComparer.Default.Equals(localRef.Local.Type, jsonSerializerOptionsSymbol))
                        {
                            localSymbols ??= new List<ILocalSymbol>();
                            localSymbols.Add(localRef.Local);
                        }

                        tmpOperation = assignment.Parent;
                    }

                    return localSymbols != null;
                }
                // For cases like:
                // var options = new JsonSerializerOptions();
                else if (currentOperation is IVariableDeclarationOperation declaration)
                {
                    if (operation.Parent is IAssignmentOperation assignment)
                    {
                        foreach (IOperation children in assignment.Children)
                        {
                            if (children is IFieldReferenceOperation or IPropertyReferenceOperation)
                            {
                                return false;
                            }
                        }
                    }

                    var local = GetLocalSymbolFromDeclaration(declaration);
                    if (local != null && SymbolEqualityComparer.Default.Equals(local.Type, jsonSerializerOptionsSymbol))
                    {
                        localSymbols = new List<ILocalSymbol> { local };
                    }

                    return localSymbols != null;
                }

                currentOperation = currentOperation.Parent;
            }

            return false;
        }

        private static ILocalSymbol? GetLocalSymbolFromDeclaration(IVariableDeclarationOperation declaration)
        {
            if (declaration.Declarators.Length != 1)
            {
                return null;
            }

            IVariableDeclaratorOperation declarator = declaration.Declarators[0];
            return declarator.Symbol;
        }
    }
}
