// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotUseWhenAllOrWaitAllWithSingleArgument : DiagnosticAnalyzer
    {
        internal const string WhenAllRuleId = "CA1842";
        internal const string WaitAllRuleId = "CA1843";

        internal static readonly DiagnosticDescriptor WhenAllRule = DiagnosticDescriptorHelper.Create(WhenAllRuleId,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWhenAllWithSingleTaskTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWhenAllWithSingleTaskTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWhenAllWithSingleTaskDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor WaitAllRule = DiagnosticDescriptorHelper.Create(WaitAllRuleId,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWaitAllWithSingleTaskTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWaitAllWithSingleTaskTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseWaitAllWithSingleTaskDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(WhenAllRule, WaitAllRule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask, out var taskType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1, out var genericTaskType))
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var invocation = (IInvocationOperation)context.Operation;
                    if (IsWhenOrWaitAllMethod(invocation.TargetMethod, taskType) &&
                        IsSingleTaskArgument(invocation, taskType, genericTaskType))
                    {
                        switch (invocation.TargetMethod.Name)
                        {
                            case nameof(Task.WhenAll):
                                context.ReportDiagnostic(invocation.CreateDiagnostic(WhenAllRule));
                                break;

                            case nameof(Task.WaitAll):
                                context.ReportDiagnostic(invocation.CreateDiagnostic(WaitAllRule));
                                break;

                            default:
                                throw new InvalidOperationException($"Unexpected method name: {invocation.TargetMethod.Name}");
                        }
                    }
                }, OperationKind.Invocation);
            });
        }

        private static bool IsWhenOrWaitAllMethod(IMethodSymbol targetMethod, INamedTypeSymbol taskType)
        {
            var nameMatches = targetMethod.Name is (nameof(Task.WhenAll)) or (nameof(Task.WaitAll));
            var parameters = targetMethod.Parameters;

            return nameMatches &&
                targetMethod.IsStatic &&
                SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, taskType) &&
                parameters.Length == 1 &&
                parameters[0].IsParams;
        }

        private static bool IsSingleTaskArgument(IInvocationOperation invocation, INamedTypeSymbol taskType, INamedTypeSymbol genericTaskType)
        {
            if (invocation.Arguments.Length != 1)
            {
                return false;
            }

            var argument = invocation.Arguments.Single();

            // Task.WhenAll and Task.WaitAll have params arguments, which are implicit
            // array creation for cases where params were passed in without explicitly
            // being an array already. 
            if (argument.Value is not IArrayCreationOperation
                {
                    IsImplicit: true,
                    Initializer: { ElementValues: { Length: 1 } initializerValues }
                })
            {
                return false;
            }

            if (initializerValues.Single().Type is not INamedTypeSymbol namedTypeSymbol)
            {
                return false;
            }

            return namedTypeSymbol.Equals(taskType, SymbolEqualityComparer.Default) ||
                namedTypeSymbol.ConstructedFrom.Equals(genericTaskType, SymbolEqualityComparer.Default);
        }
    }
}
