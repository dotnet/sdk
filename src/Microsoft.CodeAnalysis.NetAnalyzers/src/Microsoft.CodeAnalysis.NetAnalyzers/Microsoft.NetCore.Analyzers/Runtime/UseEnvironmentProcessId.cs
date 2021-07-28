// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>CA1837: Use Environment.ProcessId.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseEnvironmentProcessId : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1837";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseEnvironmentProcessIdTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseEnvironmentProcessIdMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseEnvironmentProcessIdDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Only analyze if we have Process and Environment
                if (!compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsProcess, out var processType) ||
                    !compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEnvironment, out var environmentType))
                {
                    return;
                }

                // And only if we have Process.GetCurrentProcess, Process.Id, and Environment.ProcessId.
                var processMembers = processType.GetMembers();
                var processGetCurrentProcessSymbol = processMembers.FirstOrDefault(m => m.Name == "GetCurrentProcess");
                var processIdSymbol = processMembers.FirstOrDefault(m => m.Name == "Id");
                var environmentProcessIdSymbol = environmentType.GetMembers("ProcessId").FirstOrDefault();
                if (processGetCurrentProcessSymbol is null || processIdSymbol is null || environmentProcessIdSymbol is null)
                {
                    return;
                }

                compilationContext.RegisterOperationAction(operationContext =>
                {
                    // Warn if this is `Process.GetCurrentProcess().Id`
                    var processIdPropertyReference = (IPropertyReferenceOperation)operationContext.Operation;
                    if (processIdSymbol.Equals(processIdPropertyReference.Property) &&
                        processIdPropertyReference.Instance is IInvocationOperation getCurrentProcessMethodCall &&
                        processGetCurrentProcessSymbol.Equals(getCurrentProcessMethodCall.TargetMethod))
                    {
                        operationContext.ReportDiagnostic(processIdPropertyReference.CreateDiagnostic(Rule));
                    }
                }, OperationKind.PropertyReference);
            });
        }
    }
}