﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2247: <inheritdoc cref="DoNotCreateTaskCompletionSourceWithWrongArgumentsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCreateTaskCompletionSourceWithWrongArguments : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2247";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            CreateLocalizableResourceString(nameof(DoNotCreateTaskCompletionSourceWithWrongArgumentsTitle)),
            CreateLocalizableResourceString(nameof(DoNotCreateTaskCompletionSourceWithWrongArgumentsMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(DoNotCreateTaskCompletionSourceWithWrongArgumentsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Only analyze if we can find TCS<T> and TaskContinuationOptions
                if (compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskCompletionSource1, out var tcsGenericType) &&
                    compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskContinuationOptions, out var taskContinutationOptionsType))
                {
                    // Also optionally look for the non-generic TCS, but don't require it.
                    var tcsType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskCompletionSource);

                    compilationContext.RegisterOperationAction(operationContext =>
                    {
                        IConversionOperation? conversionOperation = null;
                        switch (operationContext.Operation.Kind)
                        {
                            case OperationKind.ObjectCreation:
                                // `new TCS(object)` with an expression of type `TaskContinuationOptions` as the argument
                                var objectCreation = (IObjectCreationOperation)operationContext.Operation;
                                conversionOperation = MatchInvalidContinuationOptions(objectCreation.Constructor, objectCreation.Arguments);
                                break;

                            case OperationKind.Invocation:
                                // `base(object)` to TCS with an expression of type `TaskContinuationOptions` as the argument
                                var invocation = (IInvocationOperation)operationContext.Operation;
                                conversionOperation = MatchInvalidContinuationOptions(invocation.TargetMethod, invocation.Arguments);
                                break;
                        }

                        if (conversionOperation is not null)
                        {
                            operationContext.ReportDiagnostic(conversionOperation.CreateDiagnostic(Rule));
                        }
                    }, OperationKind.ObjectCreation, OperationKind.Invocation);

                    IConversionOperation? MatchInvalidContinuationOptions(IMethodSymbol? targetMethod, ImmutableArray<IArgumentOperation> arguments) =>
                        targetMethod != null &&
                        (targetMethod.ContainingType.OriginalDefinition.Equals(tcsGenericType) || (tcsType != null && targetMethod.ContainingType.OriginalDefinition.Equals(tcsType))) &&
                        targetMethod.Parameters.Length == 1 &&
                        targetMethod.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                        arguments.Length == 1 &&
                        arguments[0].Value is IConversionOperation conversionOperation &&
                        conversionOperation.Operand.Type != null &&
                        conversionOperation.Operand.Type.Equals(taskContinutationOptionsType) ?
                        conversionOperation : null;
                }
            });
        }
    }
}