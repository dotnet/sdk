// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseDefaultDllImportSearchPathsAttribute : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor UseDefaultDllImportSearchPathsAttributeRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5392",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.UseDefaultDllImportSearchPathsAttribute),
            nameof(MicrosoftNetCoreAnalyzersResources.UseDefaultDllImportSearchPathsAttributeMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.UseDefaultDllImportSearchPathsAttributeDescription));
        internal static DiagnosticDescriptor DoNotUseUnsafeDllImportSearchPathRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5393",
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseUnsafeDllImportSearchPath),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseUnsafeDllImportSearchPathMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseUnsafeDllImportSearchPathDescription));

        // DllImportSearchPath.AssemblyDirectory = 2.
        // DllImportSearchPath.UseDllDirectoryForDependencies = 256.
        // DllImportSearchPath.ApplicationDirectory = 512.
        private const int UnsafeBits = 2 | 256 | 512;
        private const int LegacyBehavior = 0;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            UseDefaultDllImportSearchPathsAttributeRule,
            DoNotUseUnsafeDllImportSearchPathRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var compilation = compilationStartAnalysisContext.Compilation;
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesDllImportAttribute, out INamedTypeSymbol? dllImportAttributeTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesDefaultDllImportSearchPathsAttribute, out INamedTypeSymbol? defaultDllImportSearchPathsAttributeTypeSymbol) ||
                    compilationStartAnalysisContext.Compilation.SyntaxTrees.FirstOrDefault() is not SyntaxTree tree)
                {
                    return;
                }

                var cancellationToken = compilationStartAnalysisContext.CancellationToken;
                var unsafeDllImportSearchPathBits = compilationStartAnalysisContext.Options.GetUnsignedIntegralOptionValue(
                    optionName: EditorConfigOptionNames.UnsafeDllImportSearchPathBits,
                    rule: DoNotUseUnsafeDllImportSearchPathRule,
                    tree,
                    compilationStartAnalysisContext.Compilation,
                    defaultValue: UnsafeBits);
                var defaultDllImportSearchPathsAttributeOnAssembly = compilation.Assembly.GetAttributes().FirstOrDefault(o => o.AttributeClass.Equals(defaultDllImportSearchPathsAttributeTypeSymbol));

                compilationStartAnalysisContext.RegisterSymbolAction(symbolAnalysisContext =>
                {
                    var symbol = symbolAnalysisContext.Symbol;

                    if (!symbol.IsExtern || !symbol.IsStatic)
                    {
                        return;
                    }

                    var dllImportAttribute = symbol.GetAttributes().FirstOrDefault(s => s.AttributeClass.Equals(dllImportAttributeTypeSymbol));
                    var defaultDllImportSearchPathsAttribute = symbol.GetAttributes().FirstOrDefault(s => s.AttributeClass.Equals(defaultDllImportSearchPathsAttributeTypeSymbol));

                    if (dllImportAttribute != null)
                    {
                        var constructorArguments = dllImportAttribute.ConstructorArguments;

                        if (constructorArguments.IsEmpty)
                        {
                            return;
                        }

                        if (Path.IsPathRooted(constructorArguments[0].Value.ToString()))
                        {
                            return;
                        }

                        var rule = UseDefaultDllImportSearchPathsAttributeRule;
                        var ruleArgument = symbol.Name;
                        var validatedDefaultDllImportSearchPathsAttribute = defaultDllImportSearchPathsAttribute ?? defaultDllImportSearchPathsAttributeOnAssembly;

                        if (validatedDefaultDllImportSearchPathsAttribute != null)
                        {
                            var dllImportSearchPath = (int)validatedDefaultDllImportSearchPathsAttribute.ConstructorArguments.FirstOrDefault().Value;
                            var validBits = dllImportSearchPath & unsafeDllImportSearchPathBits;

                            if (dllImportSearchPath != LegacyBehavior &&
                                validBits == 0)
                            {
                                return;
                            }

                            rule = DoNotUseUnsafeDllImportSearchPathRule;
                            ruleArgument = ((DllImportSearchPath)validBits).ToString();
                        }

                        symbolAnalysisContext.ReportDiagnostic(
                            symbol.CreateDiagnostic(
                                rule,
                                ruleArgument));
                    }
                }, SymbolKind.Method);
            });
        }
    }
}
