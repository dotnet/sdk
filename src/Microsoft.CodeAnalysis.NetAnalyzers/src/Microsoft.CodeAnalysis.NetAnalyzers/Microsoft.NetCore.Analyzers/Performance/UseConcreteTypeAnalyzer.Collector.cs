// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public partial class UseConcreteTypeAnalyzer
    {
        private sealed class Collector
        {
            private static readonly ObjectPool<Collector> _pool = new(() => new Collector());

            public ConcurrentDictionary<IFieldSymbol, bool> VirtualDispatchFields { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<ILocalSymbol, bool> VirtualDispatchLocals { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IParameterSymbol, bool> VirtualDispatchParameters { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IMethodSymbol, bool> MethodsAssignedToDelegate { get; } = new(SymbolEqualityComparer.Default);

            public ConcurrentDictionary<IFieldSymbol, PooledConcurrentSet<ITypeSymbol>> FieldAssignments { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<ILocalSymbol, PooledConcurrentSet<ITypeSymbol>> LocalAssignments { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IParameterSymbol, PooledConcurrentSet<ITypeSymbol>> ParameterAssignments { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IMethodSymbol, PooledConcurrentSet<ITypeSymbol>> MethodReturns { get; } = new(SymbolEqualityComparer.Default);

            public INamedTypeSymbol? Void { get; private set; }

            private Collector()
            {
            }

            private void Reset()
            {
                VirtualDispatchFields.Clear();
                VirtualDispatchLocals.Clear();
                VirtualDispatchParameters.Clear();
                MethodsAssignedToDelegate.Clear();

                DrainDictionary(FieldAssignments);
                DrainDictionary(LocalAssignments);
                DrainDictionary(ParameterAssignments);
                DrainDictionary(MethodReturns);

                Void = null;

                static void DrainDictionary<T>(ConcurrentDictionary<T, PooledConcurrentSet<ITypeSymbol>> d)
                {
                    foreach (var kvp in d)
                    {
                        kvp.Value.Dispose();
                    }

                    d.Clear();
                }
            }

            public static Collector GetInstance(INamedTypeSymbol voidType)
            {
                var c = _pool.Allocate();
                c.Void = voidType;
                return c;
            }

            public static void ReturnInstance(Collector c, CancellationToken cancellationToken)
            {
                c.Reset();
                _pool.Free(c, cancellationToken);
            }

            /// <summary>
            /// Identify fields/locals/params that are used as 'this' for a virtual dispatch.
            /// </summary>
            public void HandleInvocation(IInvocationOperation op)
            {
                if (op.IsVirtual)
                {
                    if (op.Instance != null)
                    {
                        var instance = op.Instance;
                        if (instance.Kind == OperationKind.ConditionalAccessInstance)
                        {
                            var parent = ((IConditionalAccessInstanceOperation)instance).GetConditionalAccess() ?? instance;
                            if (parent != null)
                            {
                                instance = ((IConditionalAccessOperation)parent).Operation;
                            }
                        }

                        switch (instance.Kind)
                        {
                            case OperationKind.FieldReference:
                                {
                                    var fieldRef = (IFieldReferenceOperation)instance;
                                    if (CanUpgrade(fieldRef.Field))
                                    {
                                        VirtualDispatchFields[fieldRef.Field] = true;
                                    }

                                    break;
                                }

                            case OperationKind.ParameterReference:
                                {
                                    var parameterRef = (IParameterReferenceOperation)instance;
                                    VirtualDispatchParameters[parameterRef.Parameter] = true;
                                    break;
                                }

                            case OperationKind.LocalReference:
                                {
                                    var localRef = (ILocalReferenceOperation)instance;
                                    VirtualDispatchLocals[localRef.Local] = true;
                                    break;
                                }
                        }
                    }
                }

                bool canUpgrade = CanUpgrade(op.TargetMethod);

                foreach (var arg in op.Arguments)
                {
                    if (arg.Value is IDelegateCreationOperation delegateOp)
                    {
                        if (delegateOp.Target is IMethodReferenceOperation methodRefOp)
                        {
                            MethodsAssignedToDelegate[methodRefOp.Method] = true;
                        }
                    }

                    if (CanUpgrade(arg.Value))
                    {
                        if (arg.Parameter != null)
                        {
                            if (arg.Parameter.RefKind is RefKind.Ref or RefKind.Out)
                            {
                                RecordAssignment(arg.Value, arg.Parameter.Type);
                            }

                            if (canUpgrade)
                            {
                                var valueTypes = GetValueTypes(arg.Value);
                                foreach (var valueType in valueTypes)
                                {
                                    RecordAssignment(arg.Parameter, valueType);
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Record the type of values assigned to each field/local/param.
            /// </summary>
            public void HandleSimpleAssignment(ISimpleAssignmentOperation op)
            {
                if (CanUpgrade(op.Target))
                {
                    var valueTypes = GetValueTypes(op.Value);
                    RecordAssignment(op.Target, valueTypes);
                }
            }

            /// <summary>
            /// Record the type of values assigned to each field/local/param.
            /// </summary>
            public void HandleCoalesceAssignment(ICoalesceAssignmentOperation op)
            {
                if (CanUpgrade(op.Target))
                {
                    var valueTypes = GetValueTypes(op.Value);
                    RecordAssignment(op.Target, valueTypes);
                }
            }

            /// <summary>
            /// Record the type of values assigned to each field/local/param.
            /// </summary>
            public void HandleDeconstructionAssignment(IDeconstructionAssignmentOperation op)
            {
                var tupleTypes = GetValueTypes(op.Value);
                foreach (var tupleType in tupleTypes.OfType<INamedTypeSymbol>())
                {
                    for (int i = 0; i < tupleType.TypeArguments.Length; i++)
                    {
                        var valueType = tupleType.TypeArguments[i];
                        if (valueType != null)
                        {
                            switch (op.Target.Kind)
                            {
                                case OperationKind.Tuple:
                                    {
                                        var tupleOp = (ITupleOperation)op.Target;
                                        if (CanUpgrade(tupleOp.Elements[i]))
                                        {
                                            RecordAssignment(tupleOp.Elements[i], valueType);
                                        }

                                        break;
                                    }

                                case OperationKind.DeclarationExpression:
                                    {
                                        var declOp = (IDeclarationExpressionOperation)op.Target;
                                        if (declOp.Expression is ITupleOperation tupleOp)
                                        {
                                            if (CanUpgrade(tupleOp.Elements[i]))
                                            {
                                                RecordAssignment(tupleOp.Elements[i], valueType);
                                            }
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Record the type of values used to initialize fields.
            /// </summary>
            public void HandleFieldInitializer(IFieldInitializerOperation op)
            {
                if (CanUpgrade(op))
                {
                    var valueTypes = GetValueTypes(op.Value);
                    foreach (var valueType in valueTypes)
                    {
                        foreach (var field in op.InitializedFields)
                        {
                            if (CanUpgrade(field))
                            {
                                RecordAssignment(field, valueType);
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Record the type of values used to initialize locals.
            /// </summary>
            public void HandleVariableDeclarator(IVariableDeclaratorOperation op)
            {
                if (op.Initializer != null && CanUpgrade(op.Initializer))
                {
                    var valueTypes = GetValueTypes(op.Initializer.Value);
                    foreach (var valueType in valueTypes)
                    {
                        RecordAssignment(op.Symbol, valueType);
                    }
                }
            }

            /// <summary>
            /// Record the type of values used to initialize locals via an expression.
            /// </summary>
            public void HandleDeclarationExpression(IDeclarationExpressionOperation op)
            {
                if (op.Expression != null && CanUpgrade(op.Expression))
                {
                    if (op.Expression.Kind == OperationKind.LocalReference)
                    {
                        var localRef = (ILocalReferenceOperation)op.Expression;
                        if (localRef.Type != null)
                        {
                            var set = LocalAssignments.GetOrAdd(localRef.Local, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default));
                            set.Add(localRef.Type);
                        }
                    }
                }
            }

            /// <summary>
            /// Record the type of values returned by a given method.
            /// </summary>
            public void HandleReturn(IReturnOperation op)
            {
                if (op.ReturnedValue != null)
                {
                    if (op.SemanticModel!.GetEnclosingSymbol(op.Syntax.SpanStart) is IMethodSymbol methodSym && CanUpgrade(methodSym))
                    {
                        var valueTypes = GetValueTypes(op.ReturnedValue);
                        foreach (var valueType in valueTypes)
                        {
                            RecordAssignment(methodSym, valueType);
                        }
                    }
                }
            }

            /// <summary>
            /// Trivial reject for types that can't be upgraded in order to avoid wasted work.
            /// </summary>
            private static bool CanUpgrade(IOperation target)
                => target.Type == null || (!target.Type.IsSealed && !target.Type.IsValueType);

            /// <summary>
            /// Trivial reject for methods that can't be upgraded in order to avoid wasted work.
            /// </summary>
            private static bool CanUpgrade(IMethodSymbol methodSym)
                => methodSym.DeclaredAccessibility == Accessibility.Private && methodSym.MethodKind == MethodKind.Ordinary;

            /// <summary>
            /// Trivial reject for fields that can't be upgraded in order to avoid wasted work.
            /// </summary>
            private static bool CanUpgrade(IFieldSymbol fieldSym)
                => fieldSym.DeclaredAccessibility == Accessibility.Private;

            private List<ITypeSymbol> GetValueTypes(IOperation op)
            {
                var values = new List<ITypeSymbol>();
                GetValueTypes(values, op);
                return values;
            }

            private void GetValueTypes(List<ITypeSymbol> values, IOperation op)
            {
                switch (op.Kind)
                {
                    case OperationKind.Literal:
                        {
                            if (op.HasNullConstantValue())
                            {
                                // use 'void' as a marker for a null assignment
                                values.Add(Void!);
                            }

                            return;
                        }

                    case OperationKind.Conversion:
                        {
                            var convOp = (IConversionOperation)op;
                            GetValueTypes(values, convOp.Operand);
                            return;
                        }

                    case OperationKind.ConditionalAccess:
                        {
                            var condOp = (IConditionalAccessOperation)op;
                            GetValueTypes(values, condOp.WhenNotNull);
                            return;
                        }

                    case OperationKind.Conditional:
                        {
                            var condOp = (IConditionalOperation)op;
                            GetValueTypes(values, condOp.WhenTrue);
                            GetValueTypes(values, condOp.WhenFalse);
                            return;
                        }

                    case OperationKind.Coalesce:
                        {
                            var colOp = (ICoalesceOperation)op;
                            GetValueTypes(values, colOp.Value);
                            GetValueTypes(values, colOp.WhenNull);
                            return;
                        }

                    case OperationKind.Invocation:
                    case OperationKind.ArrayElementReference:
                    case OperationKind.ObjectCreation:
                    case OperationKind.ParameterReference:
                    case OperationKind.PropertyReference:
                    case OperationKind.MethodReference:
                    case OperationKind.LocalReference:
                        {
                            if (op.Type != null)
                            {
                                values.Add(op.Type!);
                            }

                            return;
                        }

                    case OperationKind.FieldReference:
                        {
                            var fieldRefOp = (IFieldReferenceOperation)op;
                            if (CanUpgrade(fieldRefOp.Field))
                            {
                                values.Add(op.Type!);
                            }

                            return;
                        }

                    case OperationKind.DelegateCreation:
                        {
                            var delOp = (IDelegateCreationOperation)op;
                            if (delOp.Target is IMethodReferenceOperation target)
                            {
                                MethodsAssignedToDelegate[target.Method] = true;
                            }

                            return;
                        }

                    case OperationKind.InstanceReference:
                        {
                            var instRef = (IInstanceReferenceOperation)op;
                            values.Add(instRef.Type!);
                            return;
                        }
                }
            }

            private void RecordAssignment(IOperation op, List<ITypeSymbol> valueTypes)
            {
                foreach (var valueType in valueTypes)
                {
                    RecordAssignment(op, valueType);
                }
            }

            private void RecordAssignment(IOperation op, ITypeSymbol valueType)
            {
                switch (op.Kind)
                {
                    case OperationKind.FieldReference:
                        {
                            var fieldRef = (IFieldReferenceOperation)op;

                            // only consider fields that are being compiled, not fields from imported types
                            if (fieldRef.Field.DeclaringSyntaxReferences.Length > 0)
                            {
                                RecordAssignment(fieldRef.Field, valueType);
                            }

                            break;
                        }

                    case OperationKind.LocalReference:
                        {
                            var localRef = (ILocalReferenceOperation)op;
                            RecordAssignment(localRef.Local, valueType);
                            break;
                        }

                    case OperationKind.DeclarationExpression:
                        {
                            var declEx = (IDeclarationExpressionOperation)op;
                            RecordAssignment(declEx.Expression, valueType);
                            break;
                        }
                }
            }

            private void RecordAssignment(IFieldSymbol field, ITypeSymbol valueType) => FieldAssignments.GetOrAdd(field, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
            private void RecordAssignment(ILocalSymbol local, ITypeSymbol valueType) => LocalAssignments.GetOrAdd(local, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
            private void RecordAssignment(IParameterSymbol parameter, ITypeSymbol valueType) => ParameterAssignments.GetOrAdd(parameter.OriginalDefinition, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
            private void RecordAssignment(IMethodSymbol method, ITypeSymbol valueType) => MethodReturns.GetOrAdd(method, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
        }
    }
}
