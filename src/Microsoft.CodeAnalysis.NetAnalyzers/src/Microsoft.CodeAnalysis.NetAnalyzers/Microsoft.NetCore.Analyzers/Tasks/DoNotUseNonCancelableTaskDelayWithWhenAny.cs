// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    /// <summary>
    /// CA2027: <inheritdoc cref="DoNotUseNonCancelableTaskDelayWithWhenAnyTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseNonCancelableTaskDelayWithWhenAny : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2027";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseNonCancelableTaskDelayWithWhenAnyTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseNonCancelableTaskDelayWithWhenAnyMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(DoNotUseNonCancelableTaskDelayWithWhenAnyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;

                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask, out var taskType) ||
                    !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken, out var cancellationTokenType))
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var invocation = (IInvocationOperation)context.Operation;

                    // Check if this is a call to Task.WhenAny
                    var method = invocation.TargetMethod;
                    if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType) ||
                        !method.IsStatic ||
                        method.Name != nameof(Task.WhenAny))
                    {
                        return;
                    }

                    // Count the total number of tasks passed to WhenAny
                    int taskCount = 0;
                    List<IOperation>? taskDelayOperations = null;

                    // Task.WhenAny has params parameters, so arguments are often implicitly wrapped in an array
                    // We need to check inside the array initializer or collection expression
                    for (int i = 0; i < invocation.Arguments.Length; i++)
                    {
                        var argument = invocation.Arguments[i].Value.WalkDownConversion();

                        // Check if this is an array creation
                        if (argument is IArrayCreationOperation { Initializer: not null } arrayCreation)
                        {
                            // Check each element in the array
                            foreach (var element in arrayCreation.Initializer.ElementValues)
                            {
                                taskCount++;
                                if (IsNonCancelableTaskDelay(element, taskType, cancellationTokenType))
                                {
                                    (taskDelayOperations ??= []).Add(element);
                                }
                            }
                        }
                        else if (ICollectionExpressionOperationWrapper.IsInstance(argument))
                        {
                            // Check each element in the collection expression
                            var collectionExpression = ICollectionExpressionOperationWrapper.FromOperation(argument);
                            foreach (var element in collectionExpression.Elements)
                            {
                                taskCount++;
                                if (IsNonCancelableTaskDelay(element, taskType, cancellationTokenType))
                                {
                                    (taskDelayOperations ??= []).Add(element);
                                }
                            }
                        }
                        else
                        {
                            // Direct argument (not params or array)
                            taskCount++;
                            if (IsNonCancelableTaskDelay(argument, taskType, cancellationTokenType))
                            {
                                (taskDelayOperations ??= []).Add(argument);
                            }
                        }
                    }

                    // Only report diagnostics if there are at least 2 tasks total
                    // (avoid flagging Task.WhenAny(Task.Delay(...)) which may be used to avoid exceptions)
                    if (taskCount >= 2 && taskDelayOperations is not null)
                    {
                        foreach (var operation in taskDelayOperations)
                        {
                            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
                        }
                    }
                }, OperationKind.Invocation);
            });
        }

        private static bool IsNonCancelableTaskDelay(IOperation operation, INamedTypeSymbol taskType, INamedTypeSymbol cancellationTokenType)
        {
            operation = operation.WalkDownConversion();

            if (operation is not IInvocationOperation invocation)
            {
                return false;
            }

            // Check if this is Task.Delay
            var method = invocation.TargetMethod;
            if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType) ||
                !method.IsStatic ||
                method.Name != nameof(Task.Delay))
            {
                return false;
            }

            // Check if any parameter is a CancellationToken, in which case we consider it cancelable
            foreach (var parameter in method.Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
                {
                    return false;
                }
            }

            return true; // Task.Delay without CancellationToken
        }
    }
}
