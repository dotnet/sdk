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
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1837: <inheritdoc cref="UseEnvironmentProcessIdTitle"/>
    /// CA1839: <inheritdoc cref="UseEnvironmentProcessPathTitle"/>
    /// CA1840: <inheritdoc cref="UseEnvironmentCurrentManagedThreadIdTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseEnvironmentMembers : DiagnosticAnalyzer
    {
        internal const string EnvironmentProcessIdRuleId = "CA1837";
        internal const string EnvironmentProcessPathRuleId = "CA1839";
        internal const string EnvironmentCurrentManagedThreadIdRuleId = "CA1840";

        // Process.GetCurrentProcess().Id => Environment.ProcessId
        internal static readonly DiagnosticDescriptor UseEnvironmentProcessIdRule = DiagnosticDescriptorHelper.Create(EnvironmentProcessIdRuleId,
            CreateLocalizableResourceString(nameof(UseEnvironmentProcessIdTitle)),
            CreateLocalizableResourceString(nameof(UseEnvironmentProcessIdMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseEnvironmentProcessIdDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // Process.GetCurrentProcess().MainModule.FileName => Environment.ProcessPath
        internal static readonly DiagnosticDescriptor UseEnvironmentProcessPathRule = DiagnosticDescriptorHelper.Create(EnvironmentProcessPathRuleId,
            CreateLocalizableResourceString(nameof(UseEnvironmentProcessPathTitle)),
            CreateLocalizableResourceString(nameof(UseEnvironmentProcessPathMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseEnvironmentProcessPathDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // Thread.CurrentThread.ManagedThreadId => Environment.CurrentManagedThreadId
        internal static readonly DiagnosticDescriptor UseEnvironmentCurrentManagedThreadIdRule = DiagnosticDescriptorHelper.Create(EnvironmentCurrentManagedThreadIdRuleId,
            CreateLocalizableResourceString(nameof(UseEnvironmentCurrentManagedThreadIdTitle)),
            CreateLocalizableResourceString(nameof(UseEnvironmentCurrentManagedThreadIdMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseEnvironmentCurrentManagedThreadIdDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(UseEnvironmentProcessIdRule, UseEnvironmentProcessPathRule, UseEnvironmentCurrentManagedThreadIdRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                // Require that Environment / Process / ProcessModule / Thread exist.
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEnvironment, out var environmentType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsProcess, out var processType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsProcessModule, out var processModuleType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread, out var threadType))
                {
                    return;
                }

                // Get the various members needed from Environment, Process, ProcessModule, and Thread.
                var environmentProcessIdSymbol = environmentType.GetMembers("ProcessId").FirstOrDefault();
                var environmentProcessPathSymbol = environmentType.GetMembers("ProcessPath").FirstOrDefault();
                var environmentCurrentManagedThreadIdSymbol = environmentType.GetMembers("CurrentManagedThreadId").FirstOrDefault();
                var processGetCurrentProcessSymbol = processType.GetMembers("GetCurrentProcess").FirstOrDefault();
                var processIdSymbol = processType.GetMembers("Id").FirstOrDefault();
                var processMainModuleSymbol = processType.GetMembers("MainModule").FirstOrDefault();
                var processModuleFileNameSymbol = processModuleType.GetMembers("FileName").FirstOrDefault();
                var threadCurrentThreadSymbol = threadType.GetMembers("CurrentThread").FirstOrDefault();
                var threadManagedThreadId = threadType.GetMembers("ManagedThreadId").FirstOrDefault();

                // This one analyzer looks for multiple things, so figure out which we have the symbols to enable.
                bool lookForProcessId = processGetCurrentProcessSymbol != null && processIdSymbol != null && environmentProcessIdSymbol != null;
                bool lookForProcessPath = processGetCurrentProcessSymbol != null && processMainModuleSymbol != null && processModuleFileNameSymbol != null && environmentProcessPathSymbol != null;
                bool lookForCurrentManagedThreadId = threadCurrentThreadSymbol != null && threadManagedThreadId != null && environmentCurrentManagedThreadIdSymbol != null;
                if (!lookForProcessId && !lookForProcessPath && !lookForCurrentManagedThreadId)
                {
                    return;
                }

                // Everything we're looking for is a property, so find all property references.
                context.RegisterOperationAction(context =>
                {
                    var initialPropRef = (IPropertyReferenceOperation)context.Operation;

                    // Process.GetCurrentProcess().Id
                    if (lookForProcessId && SymbolEqualityComparer.Default.Equals(initialPropRef.Property, processIdSymbol))
                    {
                        if (initialPropRef.Instance is IInvocationOperation getCurrentProcess &&
                            SymbolEqualityComparer.Default.Equals(getCurrentProcess.TargetMethod, processGetCurrentProcessSymbol))
                        {
                            context.ReportDiagnostic(initialPropRef.CreateDiagnostic(UseEnvironmentProcessIdRule));
                        }

                        return;
                    }

                    // Process.GetCurrentProcess().MainModule.FileName
                    if (lookForProcessPath && SymbolEqualityComparer.Default.Equals(initialPropRef.Property, processModuleFileNameSymbol))
                    {
                        if (initialPropRef.Instance is IPropertyReferenceOperation mainModule &&
                            SymbolEqualityComparer.Default.Equals(mainModule.Property, processMainModuleSymbol) &&
                            mainModule.Instance is IInvocationOperation getCurrentProcess &&
                            SymbolEqualityComparer.Default.Equals(getCurrentProcess.TargetMethod, processGetCurrentProcessSymbol))
                        {
                            context.ReportDiagnostic(initialPropRef.CreateDiagnostic(UseEnvironmentProcessPathRule));
                        }

                        return;
                    }

                    // Thread.CurrentThread.ManagedThreadId
                    if (lookForCurrentManagedThreadId && SymbolEqualityComparer.Default.Equals(initialPropRef.Property, threadManagedThreadId))
                    {
                        if (initialPropRef.Instance is IPropertyReferenceOperation currentThread &&
                            SymbolEqualityComparer.Default.Equals(currentThread.Property, threadCurrentThreadSymbol))
                        {
                            context.ReportDiagnostic(initialPropRef.CreateDiagnostic(UseEnvironmentCurrentManagedThreadIdRule));
                        }

                        return;
                    }
                }, OperationKind.PropertyReference);
            });
        }
    }
}