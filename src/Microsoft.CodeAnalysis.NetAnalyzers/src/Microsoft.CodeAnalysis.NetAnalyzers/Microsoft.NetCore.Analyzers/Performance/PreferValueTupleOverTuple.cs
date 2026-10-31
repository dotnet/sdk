// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1881: <inheritdoc cref="PreferValueTupleOverTupleTitle"/>
    /// </summary>
    /// <remarks>
    /// Every place a <see cref="Tuple"/> value is created, stored, passed or returned is linked to where the value goes.
    /// A group of linked creations, locals, fields, properties, parameters and return types is reported only when none
    /// of them is ever used as anything but the source of its items, and no value enters or leaves the group through
    /// anything outside it, so the whole group can switch to a value tuple together. A null check, a reference
    /// comparison, a conversion, or a member that can't change type rules out the whole group.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferValueTupleOverTupleAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1881";
        internal const string FixableKey = "Fixable";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferValueTupleOverTupleTitle)),
            CreateLocalizableResourceString(nameof(PreferValueTupleOverTupleMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(PreferValueTupleOverTupleDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        private static readonly ImmutableDictionary<string, string?> s_fixableProperties = ImmutableDictionary<string, string?>.Empty.Add(FixableKey, null);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var compilation = context.Compilation;
            if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTuple, out var tupleClass) ||
                !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemValueTuple, out _))
            {
                return;
            }

            var createMethods = tupleClass.GetMembers("Create").OfType<IMethodSymbol>().ToImmutableHashSet<ISymbol>(SymbolEqualityComparer.Default);
            var tupleTypes = createMethods.Select(m => ((IMethodSymbol)m).ReturnType.OriginalDefinition).ToImmutableHashSet<ISymbol>(SymbolEqualityComparer.Default);
            var expressionType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqExpressionsExpression1);
            var graph = new Graph(createMethods, tupleTypes, expressionType, symbol => context.Options.MatchesConfiguredVisibility(Rule, symbol, compilation, SymbolVisibilityGroup.Private));

            context.RegisterSymbolStartAction(symbolContext =>
            {
                var topLevelType = (INamedTypeSymbol)symbolContext.Symbol;
                while (topLevelType.ContainingType is { } containingType)
                {
                    topLevelType = containingType;
                }

                // Nested types don't inherit the operation actions of the type that contains them, but the symbol end
                // of a top-level type waits for its nested types, so they all share the top-level type's nodes.
                var nodes = graph.GetNodesOf(topLevelType);

                symbolContext.RegisterOperationAction(c => graph.OnValue(c.Operation, nodes),
                    OperationKind.LocalReference, OperationKind.FieldReference, OperationKind.PropertyReference, OperationKind.ParameterReference, OperationKind.Invocation, OperationKind.ObjectCreation);
                symbolContext.RegisterOperationAction(c => graph.OnVariableDeclarator((IVariableDeclaratorOperation)c.Operation, nodes), OperationKind.VariableDeclarator);
                symbolContext.RegisterOperationAction(c => graph.OnSimpleAssignment((ISimpleAssignmentOperation)c.Operation, nodes), OperationKind.SimpleAssignment);
                symbolContext.RegisterOperationAction(c => graph.OnSymbolInitializer((ISymbolInitializerOperation)c.Operation, nodes), OperationKind.FieldInitializer, OperationKind.PropertyInitializer);
                symbolContext.RegisterOperationAction(c => graph.OnReturn((IReturnOperation)c.Operation, c.ContainingSymbol, nodes), OperationKind.Return);
                symbolContext.RegisterOperationAction(c => graph.OnArgument((IArgumentOperation)c.Operation, nodes), OperationKind.Argument);
                symbolContext.RegisterOperationAction(c => graph.OnMethodReference((IMethodReferenceOperation)c.Operation, nodes), OperationKind.MethodReference);

                if (ReferenceEquals(topLevelType, symbolContext.Symbol))
                {
                    symbolContext.RegisterSymbolEndAction(c => graph.Report(c.ReportDiagnostic, topLevelType));
                }
            }, SymbolKind.NamedType);

            context.RegisterCompilationEndAction(c => graph.Report(c.ReportDiagnostic, typeToComplete: null));
        }

        private sealed class Node
        {
            public Node(ISymbol? symbol, IOperation? creation, bool isOpen)
            {
                Parent = this;
                Symbol = symbol;
                Creation = creation;
                IsOpen = isOpen;
            }

            public Node Parent;

            // On a root: some member of the group can be referenced from outside its top-level type,
            // so the group is only complete at the end of the compilation.
            public bool IsOpen;

            // On a root: some value of the group is used in a way a value tuple can't be.
            public bool IsDisqualified;

            // On a local: it is declared by a local declaration, rather than by a pattern, a foreach or an out variable.
            public bool IsDeclared;

            public readonly ISymbol? Symbol;
            public readonly IOperation? Creation;
        }

        private sealed class Graph
        {
            private readonly object _gate = new();
            private readonly Dictionary<ISymbol, Node?> _symbolNodes = new(SymbolEqualityComparer.Default);
            // Keyed by syntax, as each operation action can be handed its own instance of the same operation.
            private readonly Dictionary<SyntaxNode, Node> _creationNodes = new();
            private readonly Dictionary<INamedTypeSymbol, List<Node>> _nodesByType = new(SymbolEqualityComparer.Default);
            private readonly ImmutableHashSet<ISymbol> _createMethods;
            private readonly ImmutableHashSet<ISymbol> _tupleTypes;
            private readonly INamedTypeSymbol? _expressionType;
            private readonly Func<ISymbol, bool> _matchesConfiguredVisibility;

            public Graph(ImmutableHashSet<ISymbol> createMethods, ImmutableHashSet<ISymbol> tupleTypes, INamedTypeSymbol? expressionType, Func<ISymbol, bool> matchesConfiguredVisibility)
            {
                _createMethods = createMethods;
                _tupleTypes = tupleTypes;
                _expressionType = expressionType;
                _matchesConfiguredVisibility = matchesConfiguredVisibility;
            }

            public List<Node> GetNodesOf(INamedTypeSymbol topLevelType)
            {
                lock (_gate)
                {
                    if (!_nodesByType.TryGetValue(topLevelType, out var nodes))
                    {
                        nodes = new List<Node>();
                        _nodesByType.Add(topLevelType, nodes);
                    }

                    return nodes;
                }
            }

            private bool IsTupleType(ITypeSymbol? type)
                => type is INamedTypeSymbol { IsGenericType: true } namedType && _tupleTypes.Contains(namedType.OriginalDefinition);

            public void OnValue(IOperation operation, List<Node> nodes)
            {
                if (!IsTupleType(operation.Type))
                {
                    return;
                }

                lock (_gate)
                {
                    if (GetValueNode(operation, nodes) is not { } node)
                    {
                        return;
                    }

                    var current = operation;
                    var isItemOfValue = false;
                    while (true)
                    {
                        var parent = current.Parent;
                        switch (parent)
                        {
                            case IParenthesizedOperation or IDeclarationExpressionOperation or IConversionOperation { Conversion.IsIdentity: true }:
                                current = parent;
                                continue;

                            // t.Item1, and t.Rest.Item1 for the eighth item
                            case IPropertyReferenceOperation propertyReference when propertyReference.Instance == current:
                                if (IsTupleType(propertyReference.Type))
                                {
                                    isItemOfValue = true;
                                    current = parent;
                                    continue;
                                }

                                return;

                            case IDeconstructionAssignmentOperation deconstruction when deconstruction.Value == current:
                            case IExpressionStatementOperation:
                                return;

                            case ISimpleAssignmentOperation assignment when assignment.Target == current:
                                if (parent.Parent is not (IExpressionStatementOperation or IObjectOrCollectionInitializerOperation))
                                {
                                    Disqualify(node);
                                }

                                return;

                            // Values flowing somewhere are linked by the handler of that flow.
                            case ISimpleAssignmentOperation or ISymbolInitializerOperation or IArgumentOperation or IReturnOperation { Kind: OperationKind.Return }:
                                if (isItemOfValue)
                                {
                                    Disqualify(node);
                                }

                                return;

                            default:
                                Disqualify(node);
                                return;
                        }
                    }
                }
            }

            public void OnVariableDeclarator(IVariableDeclaratorOperation declarator, List<Node> nodes)
            {
                if (!IsTupleType(declarator.Symbol.Type))
                {
                    return;
                }

                lock (_gate)
                {
                    var node = GetSymbolNode(declarator.Symbol, nodes)!;
                    node.IsDeclared = declarator.Parent is IVariableDeclarationOperation { Parent: IVariableDeclarationGroupOperation };
                    Link(node, declarator.GetVariableInitializer()?.Value, nodes);
                }
            }

            public void OnSimpleAssignment(ISimpleAssignmentOperation assignment, List<Node> nodes)
            {
                if (assignment.Target is IDiscardOperation || !IsTupleType(assignment.Target.Type))
                {
                    return;
                }

                lock (_gate)
                {
                    var target = assignment.Target is IMemberReferenceOperation or ILocalReferenceOperation or IParameterReferenceOperation
                        ? GetValueNode(assignment.Target, nodes)
                        : null;
                    Link(target, assignment.Value, nodes);
                }
            }

            public void OnSymbolInitializer(ISymbolInitializerOperation initializer, List<Node> nodes)
            {
                ImmutableArray<ISymbol> symbols = initializer switch
                {
                    IFieldInitializerOperation fieldInitializer => fieldInitializer.InitializedFields.CastArray<ISymbol>(),
                    IPropertyInitializerOperation propertyInitializer => propertyInitializer.InitializedProperties.CastArray<ISymbol>(),
                    _ => ImmutableArray<ISymbol>.Empty,
                };

                lock (_gate)
                {
                    foreach (var symbol in symbols)
                    {
                        if (IsTupleType(symbol.GetMemberType()))
                        {
                            Link(GetSymbolNode(symbol, nodes), initializer.Value, nodes);
                        }
                    }
                }
            }

            public void OnReturn(IReturnOperation returnOperation, ISymbol containingSymbol, List<Node> nodes)
            {
                if (returnOperation.ReturnedValue is not { } value || !IsTupleType(WithoutIdentityConversions(value).Type))
                {
                    return;
                }

                if (containingSymbol is IMethodSymbol { MethodKind: MethodKind.PropertyGet, AssociatedSymbol: { } property })
                {
                    containingSymbol = property;
                }

                lock (_gate)
                {
                    var target = returnOperation.IsWithinLambdaOrLocalFunction(out _) ? null : GetSymbolNode(containingSymbol, nodes);
                    Link(target, value, nodes);
                }
            }

            public void OnArgument(IArgumentOperation argument, List<Node> nodes)
            {
                if (!IsTupleType(WithoutIdentityConversions(argument.Value).Type))
                {
                    return;
                }

                lock (_gate)
                {
                    var target = argument.Parameter is { } parameter ? GetSymbolNode(parameter, nodes) : null;
                    Link(target, argument.Value, nodes);
                }
            }

            public void OnMethodReference(IMethodReferenceOperation methodReference, List<Node> nodes)
            {
                // A method turned into a delegate keeps its signature.
                var method = methodReference.Method;
                if (!IsTupleType(method.ReturnType) && !method.Parameters.Any(p => IsTupleType(p.Type)))
                {
                    return;
                }

                lock (_gate)
                {
                    if (GetSymbolNode(method, nodes) is { } methodNode)
                    {
                        Disqualify(methodNode);
                    }

                    foreach (var parameter in method.Parameters)
                    {
                        if (GetSymbolNode(parameter, nodes) is { } parameterNode)
                        {
                            Disqualify(parameterNode);
                        }
                    }
                }
            }

            public void Report(Action<Diagnostic> reportDiagnostic, INamedTypeSymbol? typeToComplete)
            {
                var diagnostics = new List<Diagnostic>();
                lock (_gate)
                {
                    IEnumerable<Node> candidates;
                    if (typeToComplete is not null)
                    {
                        if (!_nodesByType.TryGetValue(typeToComplete, out var nodes))
                        {
                            return;
                        }

                        _nodesByType.Remove(typeToComplete);
                        candidates = nodes;
                    }
                    else
                    {
                        candidates = _symbolNodes.Values.OfType<Node>().Concat(_creationNodes.Values);
                    }

                    // Groups with a member visible outside the type wait for the end of the compilation.
                    foreach (var group in candidates.GroupBy(Find))
                    {
                        var root = group.Key;
                        if (root.IsOpen == (typeToComplete is not null))
                        {
                            continue;
                        }

                        var members = group.Distinct().ToList();
                        foreach (var node in members)
                        {
                            if (node.Symbol is not null)
                            {
                                _symbolNodes.Remove(node.Symbol);
                            }
                            else
                            {
                                _creationNodes.Remove(node.Creation!.Syntax);
                            }
                        }

                        if (root.IsDisqualified || members.Any(n => n.Symbol is ILocalSymbol && !n.IsDeclared))
                        {
                            continue;
                        }

                        foreach (var node in members)
                        {
                            if (node.Creation is { } creation)
                            {
                                diagnostics.Add(creation.CreateDiagnostic(Rule, IsFixable(creation, members) ? s_fixableProperties : null, creation.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                            }
                            else if (node.Symbol is not ILocalSymbol)
                            {
                                diagnostics.Add(node.Symbol!.CreateDiagnostic(Rule, node.Symbol!.GetMemberOrLocalOrParameterType()!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                            }
                        }
                    }
                }

                foreach (var diagnostic in diagnostics)
                {
                    reportDiagnostic(diagnostic);
                }
            }

            // Tuple.Create(...) can be rewritten on its own when it is the whole group, or when the only other member is
            // the local it initializes.
            private bool IsFixable(IOperation creation, List<Node> members)
                => creation is IInvocationOperation { Arguments.Length: >= 2 } &&
                    creation.Parent is not IExpressionStatementOperation &&
                    !creation.IsWithinExpressionTree(_expressionType) &&
                    (members.Count == 1 ||
                     (members.Count == 2 &&
                      creation.Parent is IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } &&
                      members.Any(n => SymbolEqualityComparer.Default.Equals(n.Symbol, declarator.Symbol))));

            private void Link(Node? target, IOperation? value, List<Node> nodes)
            {
                if (value is null)
                {
                    return;
                }

                var source = GetValueNode(WithoutIdentityConversions(value), nodes);
                if (target is null)
                {
                    if (source is not null)
                    {
                        Disqualify(source);
                    }
                }
                else if (source is null)
                {
                    Disqualify(target);
                }
                else
                {
                    var targetRoot = Find(target);
                    var sourceRoot = Find(source);
                    if (targetRoot != sourceRoot)
                    {
                        sourceRoot.Parent = targetRoot;
                        targetRoot.IsOpen |= sourceRoot.IsOpen;
                        targetRoot.IsDisqualified |= sourceRoot.IsDisqualified;
                    }
                }
            }

            private void Disqualify(Node node) => Find(node).IsDisqualified = true;

            private static Node Find(Node node)
            {
                while (node.Parent != node)
                {
                    node.Parent = node.Parent.Parent;
                    node = node.Parent;
                }

                return node;
            }

            private static IOperation WithoutIdentityConversions(IOperation operation)
            {
                while (operation is IParenthesizedOperation or IDeclarationExpressionOperation or IConversionOperation { Conversion.IsIdentity: true })
                {
                    operation = operation switch
                    {
                        IParenthesizedOperation parenthesized => parenthesized.Operand,
                        IDeclarationExpressionOperation declaration => declaration.Expression,
                        _ => ((IConversionOperation)operation).Operand,
                    };
                }

                return operation;
            }

            private Node? GetValueNode(IOperation operation, List<Node> nodes)
            {
                switch (operation)
                {
                    case IInvocationOperation invocation when _createMethods.Contains(invocation.TargetMethod.OriginalDefinition):
                    case IObjectCreationOperation when IsTupleType(operation.Type):
                        if (!_creationNodes.TryGetValue(operation.Syntax, out var node))
                        {
                            node = new Node(symbol: null, operation, isOpen: false);
                            _creationNodes.Add(operation.Syntax, node);
                            nodes.Add(node);
                        }

                        return node;

                    case IInvocationOperation invocation:
                        return GetSymbolNode(invocation.TargetMethod, nodes);
                    case ILocalReferenceOperation localReference:
                        return GetSymbolNode(localReference.Local, nodes);
                    case IParameterReferenceOperation parameterReference:
                        return GetSymbolNode(parameterReference.Parameter, nodes);
                    case IMemberReferenceOperation memberReference:
                        return GetSymbolNode(memberReference.Member, nodes);
                    default:
                        return null;
                }
            }

            // Null when the symbol can't change from Tuple to ValueTuple.
            private Node? GetSymbolNode(ISymbol symbol, List<Node> nodes)
            {
                symbol = symbol.OriginalDefinition;

                // The value parameter of a setter is the property.
                if (symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.PropertySet, AssociatedSymbol: { } property } })
                {
                    symbol = property;
                }

                if (!_symbolNodes.TryGetValue(symbol, out var node))
                {
                    if (IsTupleType(symbol.GetMemberOrLocalOrParameterType()) && (symbol is ILocalSymbol || CanChangeType(symbol)))
                    {
                        node = new Node(symbol, creation: null, isOpen: symbol.GetResultantVisibility() != SymbolVisibility.Private);
                        nodes.Add(node);
                    }

                    _symbolNodes.Add(symbol, node);
                }

                return node;
            }

            private bool CanChangeType(ISymbol symbol)
            {
                switch (symbol)
                {
                    case IParameterSymbol parameter:
                        return !parameter.HasExplicitDefaultValue && parameter.ContainingSymbol is IMethodSymbol containingMethod && CanChangeType(containingMethod);
                    case IMethodSymbol method when method.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor) || method.PartialDefinitionPart is not null || method.PartialImplementationPart is not null:
                    case IPropertySymbol { IsIndexer: true }:
                    case IFieldSymbol { IsVolatile: true }:
                        return false;
                    default:
                        return symbol.IsInSource() &&
                            !symbol.IsImplicitlyDeclared &&
                            !symbol.IsVirtual &&
                            !symbol.IsAbstract &&
                            !symbol.IsOverride &&
                            !symbol.IsExtern &&
                            symbol.ContainingType.TypeKind != TypeKind.Interface &&
                            !symbol.IsImplementationOfAnyInterfaceMember() &&
                            _matchesConfiguredVisibility(symbol);
                }
            }
        }
    }
}
