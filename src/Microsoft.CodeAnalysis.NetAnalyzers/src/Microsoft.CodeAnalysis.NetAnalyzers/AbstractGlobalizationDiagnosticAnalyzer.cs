// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.NetAnalyzers
{
    public abstract class AbstractGlobalizationDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        protected virtual GeneratedCodeAnalysisFlags GeneratedCodeAnalysisFlags { get; } = GeneratedCodeAnalysisFlags.None;

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags);
            context.RegisterCompilationStartAction(context =>
            {
                var value = context.Options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.InvariantGlobalization, context.Compilation);
                if (value != "true")
                {
                    InitializeWorker(context);
                }
            });
        }

        protected abstract void InitializeWorker(CompilationStartAnalysisContext context);
    }
}
