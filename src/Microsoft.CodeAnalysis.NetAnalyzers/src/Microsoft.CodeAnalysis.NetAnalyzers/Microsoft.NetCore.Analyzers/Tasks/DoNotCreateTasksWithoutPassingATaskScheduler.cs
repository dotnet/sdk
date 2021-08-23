// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    /// <summary>
    /// CA2008: Do not create tasks without passing a TaskScheduler
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCreateTasksWithoutPassingATaskSchedulerAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2008";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotCreateTasksWithoutPassingATaskSchedulerTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotCreateTasksWithoutPassingATaskSchedulerMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotCreateTasksWithoutPassingATaskSchedulerDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.CandidateForRemoval,     // Superseded by VS threading analyzers
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: false,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Check if TPL is available before actually doing the searches
                var taskType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                var taskFactoryType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskFactory);
                var taskSchedulerType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskScheduler);
                if (taskType == null || taskFactoryType == null || taskSchedulerType == null)
                {
                    return;
                }

                compilationContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    if (invocation.TargetMethod == null)
                    {
                        return;
                    }

                    if (!IsMethodOfInterest(invocation.TargetMethod, taskType, taskFactoryType))
                    {
                        return;
                    }

                    // We want to ensure that all overloads called are explicitly taking a task scheduler
                    if (invocation.TargetMethod.Parameters.Any(p => p.Type.Equals(taskSchedulerType)))
                    {
                        return;
                    }

                    operationContext.ReportDiagnostic(invocation.CreateDiagnostic(Rule, invocation.TargetMethod.Name));
                }, OperationKind.Invocation);
            });
        }

        private static bool IsMethodOfInterest(IMethodSymbol methodSymbol, INamedTypeSymbol taskType, INamedTypeSymbol taskFactoryType)
        {
            // Check if it's a method of Task or a derived type (for Task<T>)
            if ((taskType.Equals(methodSymbol.ContainingType) ||
                 taskType.Equals(methodSymbol.ContainingType.BaseType)) &&
                methodSymbol.Name == "ContinueWith")
            {
                return true;
            }

            if (methodSymbol.ContainingType.Equals(taskFactoryType) &&
                methodSymbol.Name == "StartNew")
            {
                return true;
            }

            return false;
        }
    }
}