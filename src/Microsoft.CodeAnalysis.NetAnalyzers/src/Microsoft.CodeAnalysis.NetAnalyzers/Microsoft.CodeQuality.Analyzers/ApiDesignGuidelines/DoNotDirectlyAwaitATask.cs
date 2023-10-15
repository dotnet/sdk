// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2007: <inheritdoc cref="DoNotDirectlyAwaitATaskTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDirectlyAwaitATaskAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2007";

        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotDirectlyAwaitATaskTitle)),
            CreateLocalizableResourceString(nameof(DoNotDirectlyAwaitATaskMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotDirectlyAwaitATaskDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (context.Compilation.SyntaxTrees.FirstOrDefault() is not SyntaxTree tree ||
                    !context.Options.GetOutputKindsOption(Rule, tree, context.Compilation).Contains(context.Compilation.Options.OutputKind))
                {
                    // Configured to skip analysis for the compilation's output kind
                    return;
                }

                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                if (!TryGetTaskTypes(wellKnownTypeProvider, out ImmutableArray<INamedTypeSymbol> taskTypes))
                {
                    return;
                }

                var configuredAsyncDisposable = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesConfiguredAsyncDisposable);
                var configuredAsyncEnumerable = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesConfiguredCancelableAsyncEnumerable);

                context.RegisterOperationBlockStartAction(context =>
                {
                    if (context.OwningSymbol is IMethodSymbol method)
                    {
                        if (method.IsAsync &&
                            method.ReturnsVoid &&
                            context.Options.GetBoolOptionValue(
                                optionName: EditorConfigOptionNames.ExcludeAsyncVoidMethods,
                                rule: Rule,
                                method,
                                context.Compilation,
                                defaultValue: false))
                        {
                            // Configured to skip this analysis in async void methods.
                            return;
                        }

                        context.RegisterOperationAction(context => AnalyzeAwaitOperation(context, taskTypes), OperationKind.Await);
                        if (configuredAsyncDisposable is not null)
                        {
                            context.RegisterOperationAction(context => AnalyzeUsingOperation(context, configuredAsyncDisposable), OperationKind.Using);
                            context.RegisterOperationAction(context => AnalyzeUsingDeclarationOperation(context, configuredAsyncDisposable), OperationKind.UsingDeclaration);
                        }

                        if (configuredAsyncEnumerable is not null)
                        {
                            context.RegisterOperationAction(ctx => AnalyzeAwaitForEachLoopOperation(ctx, configuredAsyncEnumerable), OperationKind.Loop);
                        }
                    }
                });
            });
        }

        private static void AnalyzeAwaitForEachLoopOperation(OperationAnalysisContext context, INamedTypeSymbol configuredAsyncEnumerable)
        {
            if (context.Operation is IForEachLoopOperation { IsAsynchronous: true, Collection.Type: not null } forEachOperation
                && !forEachOperation.Collection.Type.OriginalDefinition.Equals(configuredAsyncEnumerable, SymbolEqualityComparer.Default))
            {
                context.ReportDiagnostic(forEachOperation.Collection.CreateDiagnostic(Rule));
            }
        }

        private static void AnalyzeAwaitOperation(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> taskTypes)
        {
            var awaitExpression = (IAwaitOperation)context.Operation;

            // Get the type of the expression being awaited and check it's a task type.
            ITypeSymbol? typeOfAwaitedExpression = awaitExpression.Operation.Type;
            if (typeOfAwaitedExpression != null && taskTypes.Contains(typeOfAwaitedExpression.OriginalDefinition))
            {
                context.ReportDiagnostic(awaitExpression.Operation.Syntax.CreateDiagnostic(Rule));
            }
        }

        private static void AnalyzeUsingOperation(OperationAnalysisContext context, INamedTypeSymbol configuredAsyncDisposable)
        {
            var usingExpression = (IUsingOperation)context.Operation;
            if (!usingExpression.IsAsynchronous)
            {
                return;
            }

            if (usingExpression.Resources is IVariableDeclarationGroupOperation variableDeclarationGroup)
            {
                foreach (var declaration in variableDeclarationGroup.Declarations)
                {
                    foreach (var declarator in declaration.Declarators)
                    {
                        // Get the type of the expression being awaited and check it's a task type.
                        if (declarator.Symbol.Type != configuredAsyncDisposable)
                        {
                            var reportingOperation = declarator.Initializer?.Value ?? declarator;
                            context.ReportDiagnostic(reportingOperation.CreateDiagnostic(Rule));
                        }
                    }
                }
            }
        }

        private static void AnalyzeUsingDeclarationOperation(OperationAnalysisContext context, INamedTypeSymbol configuredAsyncDisposable)
        {
            var usingExpression = (IUsingDeclarationOperation)context.Operation;
            if (!usingExpression.IsAsynchronous)
            {
                return;
            }

            foreach (var declaration in usingExpression.DeclarationGroup.Declarations)
            {
                foreach (var declarator in declaration.Declarators)
                {
                    // Get the type of the expression being awaited and check it's a task type.
                    if (declarator.Symbol.Type != configuredAsyncDisposable)
                    {
                        var reportingOperation = declarator.Initializer?.Value ?? declarator;
                        context.ReportDiagnostic(reportingOperation.CreateDiagnostic(Rule));
                    }
                }
            }
        }

        private static bool TryGetTaskTypes(WellKnownTypeProvider typeProvider, out ImmutableArray<INamedTypeSymbol> taskTypes)
        {
            INamedTypeSymbol? taskType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
            INamedTypeSymbol? taskOfTType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1);

            if (taskType == null || taskOfTType == null)
            {
                taskTypes = ImmutableArray<INamedTypeSymbol>.Empty;
                return false;
            }

            INamedTypeSymbol? valueTaskType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
            INamedTypeSymbol? valueTaskOfTType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1);

            taskTypes = valueTaskType != null && valueTaskOfTType != null ?
                ImmutableArray.Create(taskType, taskOfTType, valueTaskType, valueTaskOfTType) :
                ImmutableArray.Create(taskType, taskOfTType);

            return true;
        }
    }
}
