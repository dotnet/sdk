// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
