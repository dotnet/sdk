// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    using static MicrosoftCodeQualityAnalyzersResources;

    public abstract class MakeTypesInternal<TSyntaxKind> : DiagnosticAnalyzer
        where TSyntaxKind : struct, Enum
    {
        internal const string RuleId = "CA1515";

        protected static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(MakeTypesInternalTitle)),
            CreateLocalizableResourceString(nameof(MakeTypesInternalMessage)),
            DiagnosticCategory.Maintainability,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(MakeTypesInternalDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        private static readonly ImmutableHashSet<OutputKind> DefaultOutputKinds =
            ImmutableHashSet.Create(OutputKind.ConsoleApplication, OutputKind.WindowsApplication, OutputKind.WindowsRuntimeApplication);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;
                if (context.Compilation.SyntaxTrees.FirstOrDefault() is not { } firstSyntaxTree
                    || !context.Options.GetOutputKindsOption(Rule, firstSyntaxTree, compilation, DefaultOutputKinds).Contains(compilation.Options.OutputKind))
                {
                    return;
                }

                context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, TypeKinds);
                context.RegisterSyntaxNodeAction(AnalyzeEnumDeclaration, EnumKind);
                context.RegisterSyntaxNodeAction(AnalyzeDelegateDeclaration, DelegateKinds);
            });
        }

        protected abstract ImmutableArray<TSyntaxKind> TypeKinds { get; }

        protected abstract TSyntaxKind EnumKind { get; }

        protected abstract ImmutableArray<TSyntaxKind> DelegateKinds { get; }

        protected abstract void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context);

        protected abstract void AnalyzeEnumDeclaration(SyntaxNodeAnalysisContext context);

        protected abstract void AnalyzeDelegateDeclaration(SyntaxNodeAnalysisContext context);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);
    }
}