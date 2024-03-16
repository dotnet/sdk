// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Usage
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2263: <inheritdoc cref="PreferGenericOverloadsTitle"/>
    /// </summary>
    public abstract class PreferGenericOverloadsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2263";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferGenericOverloadsTitle)),
            CreateLocalizableResourceString(nameof(PreferGenericOverloadsMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferGenericOverloadsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context => context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation));
        }

        private void AnalyzeInvocation(OperationAnalysisContext context)
        {
            if (!RuntimeTypeInvocationContext.TryGetContext((IInvocationOperation)context.Operation, out var invocationContext))
            {
                return;
            }

            // Get all methods on the containing type with the same name as the original invocation that are applicable generic overloads.
            var genericInvocation = invocationContext.Method.ContainingType
                .GetMembers(invocationContext.Method.Name)
                .OfType<IMethodSymbol>()
                .Where(IsApplicableGenericOverload)
                .FirstOrDefault();

            if (genericInvocation is not null)
            {
                context.ReportDiagnostic(invocationContext.Invocation.CreateDiagnostic(
                    Rule,
                    genericInvocation.ToDisplayString(),
                    invocationContext.Method.ToDisplayString()));
            }

            // A generic overload is applicable iff:
            //   1. The arity is the same as the type parameters of the original invocation
            //   2. The parameters count is the same as the other arguments of the original invocation
            //   3. It is not the same as the containing symbol containing the original invocation
            //      This is to prevent cases where the generic method forwards to a non generic one, e.g. Foo<T>() calls Foo(typeof(T)).
            //      Without this condition we would create an infinite loop as we would replace Foo(typeof(T)) with Foo<T>().
            //   4. No nullability generic constraint is violated.
            //      We must explicitly check for this, and not rely on the speculative binding later, since it will still succeed even if a notnull constraint is violated.
            //   5. The return type is assignable to the original return type. We do not check the return type for expression statements.
            //   6. All other arguments of the original invocation are assignable to the parameters of the method.
            //   7. Speculative binding of the new invocation succeeds; this is to check if any type parameter constraints are violated.
            bool IsApplicableGenericOverload(IMethodSymbol method)
            {
                // Reduce method if original method was reduced.
                if (invocationContext.Method.ReducedFrom is not null)
                {
                    method = invocationContext.ReduceExtensionMethodOrOriginal(method, context.Compilation, context.CancellationToken);
                }

                if (method.Arity != invocationContext.TypeArguments.Length ||
                    method.Parameters.Length != invocationContext.OtherArguments.Length ||
                    SymbolEqualityComparer.Default.Equals(method, context.ContainingSymbol))
                {
                    return false;
                }

                var genericMethod = method.Construct(invocationContext.TypeArguments.ToArray());

                if (AreNullabilityConstraintsViolated(method) ||
                    !invocationContext.IsReturnTypeCompatible(genericMethod, context.Compilation) ||
                    !invocationContext.AreOtherArgumentsCompatible(genericMethod, context.Compilation) ||
                    !TryGetModifiedInvocationSyntax(invocationContext, out var modifiedInvocationSyntax))
                {
                    return false;
                }

                var speculativeSymbolInfo = invocationContext.SemanticModel?.GetSpeculativeSymbolInfo(
                    invocationContext.Syntax.SpanStart,
                    modifiedInvocationSyntax,
                    SpeculativeBindingOption.BindAsExpression);

                // Check if the expression was bound successfully.
                if (speculativeSymbolInfo?.Symbol is not IMethodSymbol boundMethod)
                {
                    return false;
                }

                // Reduce the constructed method if the bound method is reduced to be able to compare them.
                if (boundMethod.ReducedFrom is not null)
                {
                    genericMethod = invocationContext.ReduceExtensionMethodOrOriginal(genericMethod, context.Compilation, context.CancellationToken);
                }

                // Check if the speculative symbol was bound to the same method.
                // This prevents cases where we bind to a overload that was ruled out before (e.g. it is the same as the containing symbol).
                return SymbolEqualityComparer.Default.Equals(boundMethod, genericMethod)
                    && SymbolEqualityComparer.Default.Equals(boundMethod.ReturnType, genericMethod.ReturnType);

                static bool AreNullabilityConstraintsViolated(IMethodSymbol method)
                {
                    for (int i = 0; i < method.TypeParameters.Length; i++)
                    {
                        if (method.TypeParameters[i].HasNotNullConstraint &&
                            method.TypeArguments[i].CanHoldNullValue())
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        // Make the context internal to be also usable in the fixer.
        protected internal sealed class RuntimeTypeInvocationContext
        {
            private RuntimeTypeInvocationContext(
                IInvocationOperation invocation,
                ImmutableArray<ITypeSymbol> typeArguments,
                ImmutableArray<IArgumentOperation> otherArguments)
            {
                Invocation = invocation;
                TypeArguments = typeArguments;
                OtherArguments = otherArguments;
            }

            public static bool TryGetContext(IInvocationOperation invocation, [NotNullWhen(true)] out RuntimeTypeInvocationContext? invocationContext)
            {
                invocationContext = default;

                var argumentsInParameterOrder = invocation.Arguments.GetArgumentsInParameterOrder();
                var typeOfArguments = argumentsInParameterOrder.WhereAsArray(a => a.Value is ITypeOfOperation);

                // Bail out if there is no argument using the typeof operator.
                if (typeOfArguments.Length == 0)
                {
                    return false;
                }

                // Split arguments into type arguments and other arguments passed to a potential generic overload.
                var typeArguments = typeOfArguments
                    .Select(a => a.Value)
                    .OfType<ITypeOfOperation>()
                    .Select(t => t.TypeOperand)
                    .Where(t => t is not INamedTypeSymbol { IsUnboundGenericType: true })
                    .ToImmutableArray();

                // Bail out if there are no type arguments left after filtering out unbound generic types.
                if (typeArguments.Length == 0)
                {
                    return false;
                }

                var otherArguments = argumentsInParameterOrder.RemoveRange(typeOfArguments);

                invocationContext = new RuntimeTypeInvocationContext(invocation, typeArguments, otherArguments);

                return true;
            }

            public IInvocationOperation Invocation { get; }
            public ImmutableArray<ITypeSymbol> TypeArguments { get; }
            public ImmutableArray<IArgumentOperation> OtherArguments { get; }
            public SemanticModel? SemanticModel => Invocation.SemanticModel;
            public IMethodSymbol Method => Invocation.TargetMethod;
            public SyntaxNode Syntax => Invocation.Syntax;
            public IOperation? Parent => Invocation.Parent;

            public IMethodSymbol ReduceExtensionMethodOrOriginal(IMethodSymbol method, Compilation compilation, CancellationToken cancellationToken)
            {
                if (method.IsExtensionMethod)
                {
                    var receiverType = Invocation.GetReceiverType(compilation, false, cancellationToken);

                    if (receiverType is not null)
                    {
                        return method.ReduceExtensionMethod(receiverType) ?? method;
                    }
                }

                return method;
            }

            public bool IsReturnTypeCompatible(IMethodSymbol method, Compilation compilation)
            {
                // We do not care if we change the return type if it is an expression statement.
                if (Parent is IExpressionStatementOperation)
                {
                    return true;
                }

                return method.ReturnType.IsAssignableTo(Method.ReturnType, compilation);
            }

            public bool AreOtherArgumentsCompatible(IMethodSymbol method, Compilation compilation)
            {
                for (int i = 0; i < OtherArguments.Length; i++)
                {
                    if (!OtherArguments[i].Value.WalkDownConversion().Type.IsAssignableTo(method.Parameters[i].Type, compilation))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        protected abstract bool TryGetModifiedInvocationSyntax(RuntimeTypeInvocationContext invocationContext, [NotNullWhen(true)] out SyntaxNode? modifiedInvocationSyntax);
    }
}
