// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    /// <summary>CA2012: Use ValueTasks correctly.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCreateTaskCompletionSourceWithWrongArguments : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2247";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotCreateTaskCompletionSourceWithWrongArgumentsTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotCreateTaskCompletionSourceWithWrongArgumentsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotCreateTaskCompletionSourceWithWrongArgumentsDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                if (compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskCompletionSource, out var tcsType) &&
                    compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTaskContinuationOptions, out var taskContinutationOptionsType))
                {
                    compilationContext.RegisterOperationAction(operationContext =>
                    {
                        // Warn if this is `new TCS(object ...)` with an expression of type `TaskContinuationOptions` as the argument.
                        var objectCreation = (IObjectCreationOperation)operationContext.Operation;
                        if (objectCreation.Type.OriginalDefinition.Equals(tcsType) &&
                            objectCreation.Constructor.Parameters.Length != 0 && objectCreation.Constructor.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                            objectCreation.Arguments.Length != 0 && objectCreation.Arguments[0].Value is IConversionOperation conversionOperation &&
                            conversionOperation.Operand.Type.Equals(taskContinutationOptionsType))
                        {
                            operationContext.ReportDiagnostic(conversionOperation.CreateDiagnostic(Rule));
                        }
                    }, OperationKind.ObjectCreation);
                }
            });
        }
    }
}