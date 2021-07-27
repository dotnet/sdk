// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2016: Forward CancellationToken to invocations.
    /// 
    /// Conditions for positive cases:
    ///     - The containing method signature receives a ct parameter. It can be a method, a nested method, an action or a func.
    ///     - The invocation method is not receiving a ct argument, and...
    ///     - The invocation method either:
    ///         - Has no overloads but its current signature receives an optional ct=default, currently implicit, or...
    ///         - Has a method overload with the exact same arguments in the same order, plus one ct parameter at the end.
    ///         
    /// Conditions for negative cases:
    ///     - The containing method signature does not receive a ct parameter.
    ///     - The invocation method signature receives a ct and one is already being explicitly passed, or...
    ///     - The invocation method does not have an overload with the exact same arguments that also receives a ct, or...
    ///     - The invocation method only has overloads that receive more than one ct.
    ///     - The invocation method return types are not implicitly convertable to one another.
    /// </summary>
    public abstract class ForwardCancellationTokenToInvocationsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2016";

        // Try to get the name of the method from the specified invocation
        protected abstract SyntaxNode? GetInvocationMethodNameNode(SyntaxNode invocationNode);

        // Check if any of the other arguments is implicit or a named argument
        protected abstract bool ArgumentsImplicitOrNamed(INamedTypeSymbol cancellationTokenType, ImmutableArray<IArgumentOperation> arguments);

        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ForwardCancellationTokenToInvocationsDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ForwardCancellationTokenToInvocationsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ForwardCancellationTokenToInvocationsTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor ForwardCancellationTokenToInvocationsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false
        );

        internal const string ShouldFix = "ShouldFix";
        internal const string ArgumentName = "ArgumentName";
        internal const string ParameterName = "ParameterName";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(ForwardCancellationTokenToInvocationsRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context => AnalyzeCompilationStart(context));
        }

        private void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken, out INamedTypeSymbol? cancellationTokenType))
            {
                return;
            }

            // We don't care if these symbols are not defined in our compilation. They are used to special case the Task<T> <-> ValueTask<T> logic
            context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1, out INamedTypeSymbol? genericTask);
            context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1, out INamedTypeSymbol? genericValueTask);

            context.RegisterOperationAction(context =>
            {
                IInvocationOperation invocation = (IInvocationOperation)context.Operation;

                if (context.ContainingSymbol is not IMethodSymbol containingMethod)
                {
                    return;
                }

                if (!ShouldDiagnose(
                    context.Compilation,
                    invocation,
                    containingMethod,
                    cancellationTokenType,
                    genericTask,
                    genericValueTask,
                    out int shouldFix,
                    out string? cancellationTokenArgumentName,
                    out string? invocationTokenParameterName))
                {
                    return;
                }

                // Underline only the method name, if possible
                SyntaxNode? nodeToDiagnose = GetInvocationMethodNameNode(context.Operation.Syntax) ?? context.Operation.Syntax;

                ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
                properties.Add(ShouldFix, $"{shouldFix}");
                properties.Add(ArgumentName, cancellationTokenArgumentName); // The new argument to pass to the invocation
                properties.Add(ParameterName, invocationTokenParameterName); // If the passed argument should be named, then this will be non-null

                context.ReportDiagnostic(
                    nodeToDiagnose.CreateDiagnostic(
                        rule: ForwardCancellationTokenToInvocationsRule,
                        properties: properties.ToImmutable(),
                        args: new object[] { cancellationTokenArgumentName, invocation.TargetMethod.Name }));
            },
            OperationKind.Invocation);
        }

        // Determines if an invocation should trigger a diagnostic for this rule or not.
        private bool ShouldDiagnose(
            Compilation compilation,
            IInvocationOperation invocation,
            IMethodSymbol containingSymbol,
            INamedTypeSymbol cancellationTokenType,
            INamedTypeSymbol? genericTask,
            INamedTypeSymbol? genericValueTask,
            out int shouldFix, [NotNullWhen(returnValue: true)] out string? ancestorTokenParameterName, out string? invocationTokenParameterName)
        {
            shouldFix = 1;
            ancestorTokenParameterName = null;
            invocationTokenParameterName = null;

            IMethodSymbol method = invocation.TargetMethod;

            // Verify that the current invocation is not passing an explicit token already
            if (AnyArgument(invocation.Arguments,
                            a => a.Parameter.Type.Equals(cancellationTokenType) && !a.IsImplicit))
            {
                return false;
            }

            IMethodSymbol? overload = null;
            // Check if the invocation's method has either an optional implicit ct at the end not being used, or a params ct parameter at the end not being used
            if (InvocationMethodTakesAToken(method, invocation.Arguments, cancellationTokenType))
            {
                if (ArgumentsImplicitOrNamed(cancellationTokenType, invocation.Arguments))
                {
                    invocationTokenParameterName = method.Parameters[^1].Name;
                }
            }
            // or an overload that takes a ct at the end
            else if (MethodHasCancellationTokenOverload(compilation, method, cancellationTokenType, genericTask, genericValueTask, out overload))
            {
                if (ArgumentsImplicitOrNamed(cancellationTokenType, invocation.Arguments))
                {
                    invocationTokenParameterName = overload.Parameters[^1].Name;
                }
            }
            else
            {
                return false;
            }

            // Check if there is an ancestor method that has a ct that we can pass to the invocation
            if (!TryGetClosestAncestorThatTakesAToken(
                invocation, containingSymbol, cancellationTokenType,
                out shouldFix, out IMethodSymbol? ancestor, out ancestorTokenParameterName))
            {
                return false;
            }

            // Finally, if the ct is in an overload method, but adding the ancestor's ct to the current
            // invocation would cause the new signature to become a recursive call, avoid creating a diagnostic
            if (overload != null && overload == ancestor)
            {
                ancestorTokenParameterName = null;
                return false;
            }

            return true;
        }

        // Try to find the most immediate containing symbol (anonymous or local function). Returns true.
        // If none is found, return the context containing symbol. Returns false.
        private static bool TryGetClosestAncestorThatTakesAToken(
            IInvocationOperation invocation,
            IMethodSymbol containingSymbol,
            INamedTypeSymbol cancellationTokenType,
            out int shouldFix,
            [NotNullWhen(returnValue: true)] out IMethodSymbol? ancestor,
            [NotNullWhen(returnValue: true)] out string? cancellationTokenParameterName)
        {
            shouldFix = 1;
            IOperation currentOperation = invocation.Parent;
            while (currentOperation != null)
            {
                ancestor = null;

                if (currentOperation.Kind == OperationKind.AnonymousFunction)
                {
                    ancestor = ((IAnonymousFunctionOperation)currentOperation).Symbol;
                }
                else if (currentOperation.Kind == OperationKind.LocalFunction)
                {
                    ancestor = ((ILocalFunctionOperation)currentOperation).Symbol;
                }

                // When the current ancestor does not contain a ct, will continue with the next ancestor
                if (ancestor != null)
                {
                    if (TryGetTokenParamName(ancestor, cancellationTokenType, out cancellationTokenParameterName))
                    {
                        return true;
                    }
                    // If no token param was found in the previous check, return false if the current operation is an anonymous function,
                    // we don't want to keep checking the superior ancestors because the ct may be unrelated
                    if (currentOperation.Kind == OperationKind.AnonymousFunction)
                    {
                        return false;
                    }

                    // If the current operation is a local static function, and is not passing a ct, but the parent is, then the
                    // ct cannot be passed to the inner invocations of the static local method, but we want to continue trying
                    // to find the ancestor method passing a ct so that we still trigger a diagnostic, we just won't offer a fix
                    if (currentOperation.Kind == OperationKind.LocalFunction && ancestor.IsStatic)
                    {
                        shouldFix = 0;
                    }
                }

                currentOperation = currentOperation.Parent;
            }

            // Last resort: fallback to the containing symbol
            ancestor = containingSymbol;
            return TryGetTokenParamName(ancestor, cancellationTokenType, out cancellationTokenParameterName);
        }

        // Check if the method only takes one ct and is the last parameter in the method signature.
        // We want to compare the current method signature to any others with the exact same arguments in the exact same order.
        private static bool TryGetTokenParamName(
            IMethodSymbol methodDeclaration,
            INamedTypeSymbol cancellationTokenType,
            [NotNullWhen(returnValue: true)] out string? cancellationTokenParameterName)
        {
            if (methodDeclaration.Parameters.Count(x => x.Type.Equals(cancellationTokenType)) == 1 &&
                methodDeclaration.Parameters[^1] is IParameterSymbol lastParameter &&
                lastParameter.Type.Equals(cancellationTokenType)) // Covers the case when using an alias for ct
            {
                cancellationTokenParameterName = lastParameter.Name;
                return true;
            }

            cancellationTokenParameterName = null;
            return false;
        }

        // Checks if the invocation has an optional ct argument at the end or a params ct array at the end.
        private static bool InvocationMethodTakesAToken(
            IMethodSymbol method,
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol cancellationTokenType)
        {
            return
                !method.Parameters.IsEmpty &&
                method.Parameters[^1] is IParameterSymbol lastParameter &&
                (InvocationIgnoresOptionalCancellationToken(lastParameter, arguments, cancellationTokenType) ||
                InvocationIsUsingParamsCancellationToken(lastParameter, arguments, cancellationTokenType));
        }

        // Checks if the arguments enumerable has any elements that satisfy the provided condition,
        // starting the lookup with the last element since tokens tend to be added as the last argument.
        private static bool AnyArgument(ImmutableArray<IArgumentOperation> arguments, Func<IArgumentOperation, bool> predicate)
        {
            for (int i = arguments.Length - 1; i >= 0; i--)
            {
                if (predicate(arguments[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // Check if the currently used overload is the one that takes the ct, but is utilizing the default value offered in the method signature.
        // We want to offer a diagnostic for this case, so the user explicitly passes the ancestor's ct.
        private static bool InvocationIgnoresOptionalCancellationToken(
            IParameterSymbol lastParameter,
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol cancellationTokenType)
        {
            if (lastParameter.Type.Equals(cancellationTokenType) &&
                lastParameter.IsOptional) // Has a default value being used
            {
                // Find out if the ct argument is using the default value
                // Need to check among all arguments in case the user is passing them named and unordered (despite the ct being defined as the last parameter)
                return AnyArgument(
                    arguments,
                    a => a.Parameter.Type.Equals(cancellationTokenType) && a.ArgumentKind == ArgumentKind.DefaultValue);
            }
            return false;
        }

        // Checks if the method has a `params CancellationToken[]` argument in the last position and ensure no ct is being passed.
        private static bool InvocationIsUsingParamsCancellationToken(
            IParameterSymbol lastParameter,
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol cancellationTokenType)
        {
            if (lastParameter.IsParams &&
                   lastParameter.Type is IArrayTypeSymbol arrayTypeSymbol &&
                   arrayTypeSymbol.ElementType.Equals(cancellationTokenType))
            {
                IArgumentOperation? paramsArgument = arguments.FirstOrDefault(a => a.ArgumentKind == ArgumentKind.ParamArray);
                if (paramsArgument?.Value is IArrayCreationOperation arrayOperation)
                {
                    // Do not offer a diagnostic if the user already passed a ct to the params
                    return arrayOperation.Initializer.ElementValues.IsEmpty;
                }
            }

            return false;
        }

        // Check if there's a method overload with the same parameters as this one, in the same order, plus a ct at the end.
        private static bool MethodHasCancellationTokenOverload(
            Compilation compilation,
            IMethodSymbol method,
            ITypeSymbol cancellationTokenType,
            INamedTypeSymbol? genericTask,
            INamedTypeSymbol? genericValueTask,
            [NotNullWhen(returnValue: true)] out IMethodSymbol? overload)
        {
            overload = method.ContainingType
                                .GetMembers(method.Name)
                                .OfType<IMethodSymbol>()
                                .FirstOrDefault(methodToCompare =>
                HasSameParametersPlusCancellationToken(compilation, cancellationTokenType, genericTask, genericValueTask, method, methodToCompare));

            return overload != null;

            // Checks if the parameters of the two passed methods only differ in a ct.
            static bool HasSameParametersPlusCancellationToken(
                Compilation compilation,
                ITypeSymbol cancellationTokenType,
                INamedTypeSymbol? genericTask,
                INamedTypeSymbol? genericValueTask,
                IMethodSymbol originalMethod,
                IMethodSymbol methodToCompare)
            {
                // Avoid comparing to itself, or when there are no parameters, or when the last parameter is not a ct
                if (originalMethod.Equals(methodToCompare) ||
                    methodToCompare.Parameters.Count(p => p.Type.Equals(cancellationTokenType)) != 1 ||
                    !methodToCompare.Parameters[^1].Type.Equals(cancellationTokenType))
                {
                    return false;
                }

                IMethodSymbol originalMethodWithAllParameters = (originalMethod.ReducedFrom ?? originalMethod).OriginalDefinition;
                IMethodSymbol methodToCompareWithAllParameters = (methodToCompare.ReducedFrom ?? methodToCompare).OriginalDefinition;

                // Ensure parameters only differ by one - the ct
                if (originalMethodWithAllParameters.Parameters.Length != methodToCompareWithAllParameters.Parameters.Length - 1)
                {
                    return false;
                }

                // Now compare the types of all parameters before the ct
                // The largest i is the number of parameters in the method that has fewer parameters
                for (int i = 0; i < originalMethodWithAllParameters.Parameters.Length; i++)
                {
                    IParameterSymbol originalParameter = originalMethodWithAllParameters.Parameters[i];
                    IParameterSymbol comparedParameter = methodToCompareWithAllParameters.Parameters[i];
                    if (!originalParameter.Type.Equals(comparedParameter.Type))
                    {
                        return false;
                    }
                }

                // Overload is  valid if its return type is implicitly convertable
                var toCompareReturnType = methodToCompareWithAllParameters.ReturnType;
                var originalReturnType = originalMethodWithAllParameters.ReturnType;
                if (!toCompareReturnType.IsAssignableTo(originalReturnType, compilation))
                {
                    // Generic Task-like types are special since awaiting them essentially erases the task-like type.
                    // If both types are Task-like we will warn if their generic arguments are convertable to each other.
                    if (IsTaskLikeType(originalReturnType) && IsTaskLikeType(toCompareReturnType) &&
                        originalReturnType is INamedTypeSymbol originalNamedType &&
                        toCompareReturnType is INamedTypeSymbol toCompareNamedType &&
                        TypeArgumentsAreConvertable(originalNamedType, toCompareNamedType))
                    {
                        return true;
                    }

                    return false;
                }

                return true;

                bool IsTaskLikeType(ITypeSymbol typeSymbol)
                {
                    if (genericTask is not null &&
                        typeSymbol.OriginalDefinition.Equals(genericTask))
                    {
                        return true;
                    }

                    if (genericValueTask is not null &&
                        typeSymbol.OriginalDefinition.Equals(genericValueTask))
                    {
                        return true;
                    }

                    return false;
                }

                bool TypeArgumentsAreConvertable(INamedTypeSymbol left, INamedTypeSymbol right)
                {
                    if (left.Arity != 1 ||
                        right.Arity != 1 ||
                        left.Arity != right.Arity)
                    {
                        return false;
                    }

                    var leftTypeArgument = left.TypeArguments[0];
                    var rightTypeArgument = right.TypeArguments[0];
                    if (!leftTypeArgument.IsAssignableTo(rightTypeArgument, compilation))
                    {
                        return false;
                    }

                    return true;
                }
            }
        }
    }
}