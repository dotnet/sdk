// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2007: Do not directly await a Task in libraries. Append ConfigureAwait(false) to the task.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDirectlyAwaitATaskAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2007";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDirectlyAwaitATaskTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDirectlyAwaitATaskMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDirectlyAwaitATaskDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(context =>
            {
                if (!(context.Compilation.SyntaxTrees.FirstOrDefault() is SyntaxTree tree) ||
                    !context.Options.GetOutputKindsOption(Rule, tree, context.Compilation, context.CancellationToken).Contains(context.Compilation.Options.OutputKind))
                {
                    // Configured to skip analysis for the compilation's output kind
                    return;
                }

                if (!TryGetTaskTypes(context.Compilation, out ImmutableArray<INamedTypeSymbol> taskTypes))
                {
                    return;
                }

                context.RegisterOperationBlockStartAction(operationBlockStartContext =>
                {
                    if (operationBlockStartContext.OwningSymbol is IMethodSymbol method)
                    {
                        if (method.IsAsync &&
                            method.ReturnsVoid &&
                            operationBlockStartContext.Options.GetBoolOptionValue(
                                optionName: EditorConfigOptionNames.ExcludeAsyncVoidMethods,
                                rule: Rule,
                                method,
                                context.Compilation,
                                defaultValue: false,
                                cancellationToken: operationBlockStartContext.CancellationToken))
                        {
                            // Configured to skip this analysis in async void methods.
                            return;
                        }

                        operationBlockStartContext.RegisterOperationAction(oc => AnalyzeOperation(oc, taskTypes), OperationKind.Await);
                    }
                });
            });
        }

        private static void AnalyzeOperation(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> taskTypes)
        {
            var awaitExpression = (IAwaitOperation)context.Operation;

            // Get the type of the expression being awaited and check it's a task type.
            ITypeSymbol? typeOfAwaitedExpression = awaitExpression.Operation.Type;
            if (typeOfAwaitedExpression != null && taskTypes.Contains(typeOfAwaitedExpression.OriginalDefinition))
            {
                context.ReportDiagnostic(awaitExpression.Operation.Syntax.CreateDiagnostic(Rule));
            }
        }

        private static bool TryGetTaskTypes(Compilation compilation, out ImmutableArray<INamedTypeSymbol> taskTypes)
        {
            INamedTypeSymbol? taskType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
            INamedTypeSymbol? taskOfTType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1);

            if (taskType == null || taskOfTType == null)
            {
                taskTypes = ImmutableArray<INamedTypeSymbol>.Empty;
                return false;
            }

            INamedTypeSymbol? valueTaskType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
            INamedTypeSymbol? valueTaskOfTType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1);

            taskTypes = valueTaskType != null && valueTaskOfTType != null ?
                ImmutableArray.Create(taskType, taskOfTType, valueTaskType, valueTaskOfTType) :
                ImmutableArray.Create(taskType, taskOfTType);

            return true;
        }
    }
}
