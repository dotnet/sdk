// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1806: Do not ignore method results
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotIgnoreMethodResultsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1806";

        private static readonly ImmutableHashSet<string> s_stringMethodNames = ImmutableHashSet.CreateRange(
            new[] {
                "ToUpper",
                "ToLower",
                "Trim",
                "TrimEnd",
                "TrimStart",
                "ToUpperInvariant",
                "ToLowerInvariant",
                "Clone",
                "Format",
                "Concat",
                "Copy",
                "Insert",
                "Join",
                "Normalize",
                "Remove",
                "Replace",
                "Split",
                "PadLeft",
                "PadRight",
                "Substring",
            });

        private static readonly ImmutableHashSet<string> s_nUnitMethodNames = ImmutableHashSet.CreateRange(
            new[] {
                "Throws",
                "Catch",
                "DoesNotThrow",
                "ThrowsAsync",
                "CatchAsync",
                "DoesNotThrowAsync"
            });

        private static readonly ImmutableHashSet<string> s_xUnitMethodNames = ImmutableHashSet.Create(
            new[] {
                "Throws",
                "ThrowsAsync",
                "ThrowsAny",
                "ThrowsAnyAsync",
            });

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageObjectCreation = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageObjectCreation), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageStringCreation = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageStringCreation), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageHResultOrErrorCode = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageHResultOrErrorCode), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessagePureMethod = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessagePureMethod), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageTryParse = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageTryParse), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageLinqMethod = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageLinqMethod), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageUserDefinedMethod = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageUserDefinedMethod), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor ObjectCreationRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageObjectCreation,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor StringCreationRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageStringCreation,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor HResultOrErrorCodeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageHResultOrErrorCode,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor PureMethodRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessagePureMethod,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor TryParseRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageTryParse,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor LinqMethodRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageLinqMethod,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor UserDefinedMethodRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageUserDefinedMethod,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ObjectCreationRule, StringCreationRule, HResultOrErrorCodeRule, TryParseRule, PureMethodRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? expectedExceptionType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedExceptionAttribute);
                INamedTypeSymbol? nunitAssertType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkAssert);
                INamedTypeSymbol? xunitAssertType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.XunitAssert);
                INamedTypeSymbol? linqEnumerableType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable);

                compilationContext.RegisterOperationBlockStartAction(osContext =>
                {
                    if (osContext.OwningSymbol is not IMethodSymbol method)
                    {
                        return;
                    }

                    osContext.RegisterOperationAction(opContext =>
                    {
                        IOperation expression = ((IExpressionStatementOperation)opContext.Operation).Operation;

                        var userDefinedMethods = compilationContext.Options.GetAdditionalUseResultsMethodsOption(UserDefinedMethodRule, expression.Syntax.SyntaxTree, compilationContext.Compilation);

                        DiagnosticDescriptor? rule = null;
                        string targetMethodName = "";
                        switch (expression.Kind)
                        {
                            case OperationKind.ObjectCreation:
                                IMethodSymbol ctor = ((IObjectCreationOperation)expression).Constructor;
                                if (ctor != null)
                                {
                                    rule = ObjectCreationRule;
                                    targetMethodName = ctor.ContainingType.Name;
                                }
                                break;

                            case OperationKind.Invocation:
                                IInvocationOperation invocationExpression = (IInvocationOperation)expression;
                                IMethodSymbol targetMethod = invocationExpression.TargetMethod;
                                if (targetMethod.ReturnsVoid)
                                {
                                    break;
                                }

                                if (IsStringCreatingMethod(targetMethod))
                                {
                                    rule = StringCreationRule;
                                }
                                else if (IsTryParseMethod(targetMethod))
                                {
                                    rule = TryParseRule;
                                }
                                else if (IsHResultOrErrorCodeReturningMethod(targetMethod))
                                {
                                    rule = HResultOrErrorCodeRule;
                                }
                                else if (IsPureMethod(targetMethod, opContext.Compilation))
                                {
                                    rule = PureMethodRule;
                                }
                                else if (targetMethod.ContainingType.Equals(linqEnumerableType))
                                {
                                    rule = LinqMethodRule;
                                }
                                else if (userDefinedMethods.Contains(targetMethod.OriginalDefinition))
                                {
                                    rule = UserDefinedMethodRule;
                                }

                                targetMethodName = targetMethod.Name;
                                break;
                        }

                        if (rule != null)
                        {
                            if (ShouldSkipAnalyzing(opContext, expectedExceptionType, xunitAssertType, nunitAssertType))
                            {
                                return;
                            }

                            Diagnostic diagnostic = expression.CreateDiagnostic(rule, method.Name, targetMethodName);
                            opContext.ReportDiagnostic(diagnostic);
                        }
                    }, OperationKind.ExpressionStatement);
                });
            });
        }

        private static bool ShouldSkipAnalyzing(OperationAnalysisContext operationContext, INamedTypeSymbol? expectedExceptionType, INamedTypeSymbol? xunitAssertType, INamedTypeSymbol? nunitAssertType)
        {
            static bool IsThrowsArgument(IParameterSymbol parameterSymbol, string argumentName, ImmutableHashSet<string> methodNames, INamedTypeSymbol? assertSymbol)
            {
                return parameterSymbol.Name == argumentName &&
                       parameterSymbol.ContainingSymbol is IMethodSymbol methodSymbol &&
                       methodNames.Contains(methodSymbol.Name) &&
                       Equals(methodSymbol.ContainingSymbol, assertSymbol);
            }

            bool IsNUnitThrowsArgument(IParameterSymbol parameterSymbol)
            {
                return IsThrowsArgument(parameterSymbol, "code", s_nUnitMethodNames, nunitAssertType);
            }

            bool IsXunitThrowsArgument(IParameterSymbol parameterSymbol)
            {
                return IsThrowsArgument(parameterSymbol, "testCode", s_xUnitMethodNames, xunitAssertType);
            }

            // We skip analysis for the last statement in a lambda passed to Assert.Throws/ThrowsAsync (xUnit and NUnit), or the last
            // statement in a method annotated with [ExpectedException] (MSTest)

            if (expectedExceptionType == null && xunitAssertType == null && nunitAssertType == null)
            {
                return false;
            }

            // Note: We do not attempt to account for a synchronously-running ThrowsAsync with something like return Task.CompletedTask;
            // as the last line.

            // We only skip analysis if we're in a method
            if (operationContext.ContainingSymbol.Kind != SymbolKind.Method)
            {
                return false;
            }

            // Get the enclosing block.
            if (operationContext.Operation.Parent is not IBlockOperation enclosingBlock)
            {
                return false;
            }

            // If enclosing block isn't the topmost IBlockOperation (MSTest case) or its parent isn't an IAnonymousFunctionOperation (xUnit/NUnit), then
            // we bail immediately
            var hasTopmostBlockParent = enclosingBlock == operationContext.Operation.GetTopmostParentBlock();
            var hasAnonymousFunctionParent = enclosingBlock.Parent?.Kind == OperationKind.AnonymousFunction;
            if (!hasTopmostBlockParent && !hasAnonymousFunctionParent)
            {
                return false;
            }

            // Only skip analyzing the last non-implicit statement in the function
            bool foundBlock = false;
            foreach (var statement in enclosingBlock.Operations)
            {
                if (statement == operationContext.Operation)
                {
                    foundBlock = true;
                }
                else if (foundBlock)
                {
                    if (!statement.IsImplicit)
                    {
                        return false;
                    }
                }
            }

            // If enclosing block is the topmost block, we're in the MSTest case. Otherwise, we're in the xUnit/NUnit case.
            if (hasTopmostBlockParent)
            {
                if (expectedExceptionType == null)
                {
                    return false;
                }

                IMethodSymbol methodSymbol = (IMethodSymbol)operationContext.ContainingSymbol;

                return methodSymbol.HasAttribute(expectedExceptionType);
            }
            else
            {
                IArgumentOperation? argumentOperation = enclosingBlock.GetAncestor<IArgumentOperation>(OperationKind.Argument);

                if (argumentOperation == null)
                {
                    return false;
                }

                return IsNUnitThrowsArgument(argumentOperation.Parameter) || IsXunitThrowsArgument(argumentOperation.Parameter);
            }
        }

        private static bool IsStringCreatingMethod(IMethodSymbol method)
        {
            return method.ContainingType.SpecialType == SpecialType.System_String &&
                s_stringMethodNames.Contains(method.Name);
        }

        private static bool IsTryParseMethod(IMethodSymbol method)
        {
            return method.Name.StartsWith("TryParse", StringComparison.OrdinalIgnoreCase) &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                method.Parameters.Length >= 2 &&
                method.Parameters[1].RefKind != RefKind.None;
        }

        private static bool IsHResultOrErrorCodeReturningMethod(IMethodSymbol method)
        {
            // Tune this method to match the FxCop behavior once https://github.com/dotnet/roslyn/issues/7282 is addressed.
            return method.GetDllImportData() != null &&
                (method.ReturnType.SpecialType == SpecialType.System_Int32 ||
                method.ReturnType.SpecialType == SpecialType.System_UInt32);
        }

        private static bool IsPureMethod(IMethodSymbol method, Compilation compilation)
        {
            return method.HasAttribute(compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsContractsPureAttribute));
        }
    }
}