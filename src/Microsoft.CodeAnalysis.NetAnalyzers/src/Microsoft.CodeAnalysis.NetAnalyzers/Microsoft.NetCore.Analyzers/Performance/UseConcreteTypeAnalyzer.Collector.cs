// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
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

            public ConcurrentDictionary<IFieldSymbol, PooledConcurrentSet<IMethodSymbol>> VirtualDispatchFields { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IPropertySymbol, PooledConcurrentSet<IMethodSymbol>> VirtualDispatchProperties { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<ILocalSymbol, PooledConcurrentSet<IMethodSymbol>> VirtualDispatchLocals { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IParameterSymbol, PooledConcurrentSet<IMethodSymbol>> VirtualDispatchParameters { get; } = new(SymbolEqualityComparer.Default);

            public ConcurrentDictionary<IFieldSymbol, PooledConcurrentSet<ITypeSymbol>> FieldAssignments { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IPropertySymbol, PooledConcurrentSet<ITypeSymbol>> PropertyAssignments { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<ILocalSymbol, PooledConcurrentSet<ITypeSymbol>> LocalAssignments { get; } = new(SymbolEqualityComparer.Default);
            public ConcurrentDictionary<IParameterSymbol, PooledConcurrentSet<ITypeSymbol>> ParameterAssignments { get; } = new(SymbolEqualityComparer.Default);

            public ConcurrentDictionary<IMethodSymbol, PooledConcurrentSet<ITypeSymbol>> MethodReturns { get; } = new(SymbolEqualityComparer.Default);

            public ConcurrentDictionary<IMethodSymbol, bool> MethodsAssignedToDelegate { get; } = new(SymbolEqualityComparer.Default);

            public INamedTypeSymbol? Void { get; private set; }
            private Func<ISymbol, bool>? _checkVisibility;

            private Collector()
            {
            }

            private void Reset()
            {
                DrainDictionary(VirtualDispatchFields);
                DrainDictionary(VirtualDispatchProperties);
                DrainDictionary(VirtualDispatchLocals);
                DrainDictionary(VirtualDispatchParameters);

                DrainDictionary(FieldAssignments);
                DrainDictionary(PropertyAssignments);
                DrainDictionary(LocalAssignments);
                DrainDictionary(ParameterAssignments);

                DrainDictionary(MethodReturns);

                MethodsAssignedToDelegate.Clear();

                Void = null;
                _checkVisibility = null;

                static void DrainDictionary<T, U>(ConcurrentDictionary<T, PooledConcurrentSet<U>> d)
                    where U : notnull
                {
                    foreach (var kvp in d)
                    {
                        kvp.Value.Dispose();
                    }

                    d.Clear();
                }
            }

            public static Collector GetInstance(INamedTypeSymbol voidType, Func<ISymbol, bool> visibilityChecker)
            {
                var c = _pool.Allocate();
                c.Void = voidType;
                c._checkVisibility = visibilityChecker;
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
                                        RecordVirtualDispatch(fieldRef.Field, op.TargetMethod);
                                    }

                                    break;
                                }

                            case OperationKind.PropertyReference:
                                {
                                    var propertyRef = (IPropertyReferenceOperation)instance;
                                    if (CanUpgrade(propertyRef.Property, false))
                                    {
                                        RecordVirtualDispatch(propertyRef.Property, op.TargetMethod);
                                    }

                                    break;
                                }

                            case OperationKind.LocalReference:
                                {
                                    var localRef = (ILocalReferenceOperation)instance;
                                    RecordVirtualDispatch(localRef.Local, op.TargetMethod);
                                    break;
                                }

                            case OperationKind.ParameterReference:
                                {
                                    var parameterRef = (IParameterReferenceOperation)instance;
                                    RecordVirtualDispatch(parameterRef.Parameter, op.TargetMethod);
                                    break;
                                }
                        }
                    }
                }

                bool canUpgradeMethod = CanUpgrade(op.TargetMethod);

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

                            if (canUpgradeMethod)
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
            /// Record the type of values used to initialize properties.
            /// </summary>
            public void HandlePropertyInitializer(IPropertyInitializerOperation op)
            {
                if (CanUpgrade(op))
                {
                    var valueTypes = GetValueTypes(op.Value);
                    foreach (var valueType in valueTypes)
                    {
                        foreach (var property in op.InitializedProperties)
                        {
                            if (CanUpgrade(property, false))
                            {
                                RecordAssignment(property, valueType);
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
            /// Record the type of values returned by a given method or property.
            /// </summary>
            public void HandleReturn(IReturnOperation op)
            {
                if (op.ReturnedValue != null)
                {
                    var c = op.SemanticModel!.GetEnclosingSymbol(op.Syntax.SpanStart);

                    if (c is IMethodSymbol methodSym)
                    {
                        if (methodSym.AssociatedSymbol is IPropertySymbol propertySym)
                        {
                            if (CanUpgrade(propertySym, false))
                            {
                                var valueTypes = GetValueTypes(op.ReturnedValue);
                                foreach (var valueType in valueTypes)
                                {
                                    RecordAssignment(propertySym, valueType);
                                }
                            }
                        }
                        else if (CanUpgrade(methodSym))
                        {
                            var valueTypes = GetValueTypes(op.ReturnedValue);
                            foreach (var valueType in valueTypes)
                            {
                                RecordReturn(methodSym, valueType);
                            }
                        }
                    }
                }
            }

            private List<ITypeSymbol> GetValueTypes(IOperation op)
            {
                var values = new List<ITypeSymbol>();
                GetValueTypes(values, op);
                return values;
            }

            private void GetValueTypes(List<ITypeSymbol> values, IOperation? op)
            {
                switch (op?.Kind)
                {
                    case OperationKind.Literal:
                        {
                            if (op.HasNullConstantValue())
                            {
                                // use 'void' as a marker for a null assignment
                                values.Add(Void!);
                            }
                            else if (op.Type != null)
                            {
                                values.Add(op.Type);
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

                            var oldCount = values.Count;
                            GetValueTypes(values, colOp.Value);

                            if (values.Count > oldCount)
                            {
                                // erase any potential nullable annotations of the left-hand value since when the value is null, it doesn't get used
                                values[^1] = values[^1].WithNullableAnnotation(CodeAnalysis.NullableAnnotation.NotAnnotated);
                            }

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
                    case OperationKind.FieldReference:
                    case OperationKind.InstanceReference:
                    case OperationKind.SwitchExpression:
                    case OperationKind.InterpolatedString:
                    case OperationKind.NameOf:
                    case OperationKind.SizeOf:
                        {
                            if (op.Type != null)
                            {
                                values.Add(op.Type);
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

                    case OperationKind.ArrayCreation:
                        {
                            var arrayOp = (IArrayCreationOperation)op;
                            if (arrayOp.Type != null)
                            {
                                values.Add(arrayOp.Type);
                            }

                            break;
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

                    case OperationKind.PropertyReference:
                        {
                            var propertyRef = (IPropertyReferenceOperation)op;

                            // only consider properties that are being compiled, not properties from imported types
                            if (propertyRef.Property.DeclaringSyntaxReferences.Length > 0)
                            {
                                RecordAssignment(propertyRef.Property, valueType);
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

            /// <summary>
            /// Removes all non-private symbols from a source collector, and puts them in 'this'.
            /// </summary>
            public void ExtractNonPrivate(Collector source)
            {
                foreach (var pair in source.VirtualDispatchFields)
                {
                    var field = pair.Key;
                    if (!field.IsPrivate())
                    {
                        source.VirtualDispatchFields.TryRemove(field, out var value);
                        foreach (var method in value)
                        {
                            RecordVirtualDispatch(field, method);
                        }
                    }
                }

                foreach (var pair in source.VirtualDispatchProperties)
                {
                    var property = pair.Key;
                    if (!property.IsPrivate())
                    {
                        source.VirtualDispatchProperties.TryRemove(property, out var value);
                        foreach (var method in value)
                        {
                            RecordVirtualDispatch(property, method);
                        }
                    }
                }

                foreach (var pair in source.VirtualDispatchParameters)
                {
                    var parameter = pair.Key;
                    if (parameter.ContainingSymbol is IMethodSymbol method)
                    {
                        if (!method.IsPrivate())
                        {
                            source.VirtualDispatchParameters.TryRemove(parameter, out var value);
                            foreach (var m in value)
                            {
                                RecordVirtualDispatch(parameter, m);
                            }
                        }
                    }
                }

                foreach (var pair in source.FieldAssignments)
                {
                    var field = pair.Key;
                    if (!field.IsPrivate())
                    {
                        source.FieldAssignments.TryRemove(field, out var value);
                        foreach (var type in value)
                        {
                            RecordAssignment(field, type);
                        }
                    }
                }

                foreach (var pair in source.PropertyAssignments)
                {
                    var property = pair.Key;
                    if (!property.IsPrivate())
                    {
                        source.PropertyAssignments.TryRemove(property, out var value);
                        foreach (var type in value)
                        {
                            RecordAssignment(property, type);
                        }
                    }
                }

                foreach (var pair in source.ParameterAssignments)
                {
                    var parameter = pair.Key;
                    if (parameter.ContainingSymbol is IMethodSymbol method)
                    {
                        if (!method.IsPrivate())
                        {
                            source.ParameterAssignments.TryRemove(parameter, out var value);
                            foreach (var type in value)
                            {
                                RecordAssignment(parameter, type);
                            }
                        }
                    }
                }

                foreach (var pair in source.MethodReturns)
                {
                    var method = pair.Key;
                    if (!method.IsPrivate())
                    {
                        source.MethodReturns.TryRemove(method, out var value);
                        foreach (var type in value)
                        {
                            RecordReturn(method, type);
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
            private bool CanUpgrade(IMethodSymbol methodSym)
                => _checkVisibility!(methodSym)
                && methodSym.MethodKind == MethodKind.Ordinary
                && !methodSym.IsImplementationOfAnyInterfaceMember()
                && !methodSym.IsOverride
                && !methodSym.IsVirtual
                && methodSym.PartialDefinitionPart == null;

            /// <summary>
            /// Trivial reject for fields that can't be upgraded in order to avoid wasted work.
            /// </summary>
            private bool CanUpgrade(IFieldSymbol fieldSym)
                => _checkVisibility!(fieldSym);

            /// <summary>
            /// Trivial reject for properties that can't be upgraded in order to avoid wasted work.
            /// </summary>
            private bool CanUpgrade(IPropertySymbol propSym, bool setter)
            {
                var m = setter ? propSym.SetMethod! : propSym.GetMethod!;

                return _checkVisibility!(m)
                    && !m.IsImplementationOfAnyInterfaceMember()
                    && !m.IsOverride
                    && !m.IsVirtual
                    && m.PartialDefinitionPart == null;
            }

            private void RecordVirtualDispatch(IFieldSymbol field, IMethodSymbol target) => VirtualDispatchFields.GetOrAdd(field, _ => PooledConcurrentSet<IMethodSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(target);
            private void RecordVirtualDispatch(IPropertySymbol property, IMethodSymbol target) => VirtualDispatchProperties.GetOrAdd(property, _ => PooledConcurrentSet<IMethodSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(target);
            private void RecordVirtualDispatch(ILocalSymbol local, IMethodSymbol target) => VirtualDispatchLocals.GetOrAdd(local, _ => PooledConcurrentSet<IMethodSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(target);
            private void RecordVirtualDispatch(IParameterSymbol parameter, IMethodSymbol target) => VirtualDispatchParameters.GetOrAdd(parameter, _ => PooledConcurrentSet<IMethodSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(target);

            private void RecordAssignment(IFieldSymbol field, ITypeSymbol valueType) => FieldAssignments.GetOrAdd(field, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
            private void RecordAssignment(IPropertySymbol property, ITypeSymbol valueType) => PropertyAssignments.GetOrAdd(property.OriginalDefinition, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
            private void RecordAssignment(ILocalSymbol local, ITypeSymbol valueType) => LocalAssignments.GetOrAdd(local, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
            private void RecordAssignment(IParameterSymbol parameter, ITypeSymbol valueType) => ParameterAssignments.GetOrAdd(parameter.OriginalDefinition, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);

            private void RecordReturn(IMethodSymbol method, ITypeSymbol valueType) => MethodReturns.GetOrAdd(method, _ => PooledConcurrentSet<ITypeSymbol>.GetInstance(SymbolEqualityComparer.Default)).Add(valueType);
        }
    }
}
