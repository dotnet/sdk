// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5392: <inheritdoc cref="UseDefaultDllImportSearchPathsAttribute"/>
    /// CA5393: <inheritdoc cref="DoNotUseUnsafeDllImportSearchPath"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseDefaultDllImportSearchPathsAttribute : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor UseDefaultDllImportSearchPathsAttributeRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5392",
            nameof(UseDefaultDllImportSearchPathsAttribute),
            nameof(UseDefaultDllImportSearchPathsAttributeMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(UseDefaultDllImportSearchPathsAttributeDescription));

        internal static readonly DiagnosticDescriptor DoNotUseUnsafeDllImportSearchPathRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5393",
            nameof(DoNotUseUnsafeDllImportSearchPath),
            nameof(DoNotUseUnsafeDllImportSearchPathMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(DoNotUseUnsafeDllImportSearchPathDescription));

        // DllImportSearchPath.AssemblyDirectory = 2.
        // DllImportSearchPath.UseDllDirectoryForDependencies = 256.
        // DllImportSearchPath.ApplicationDirectory = 512.
        private const int UnsafeBits = 2 | 256 | 512;
        private const int LegacyBehavior = 0;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
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
                var dllImportSearchDirectoryTypeIsPresent = wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemRuntimeInteropServicesDefaultDllImportSearchPathsAttribute,
                    out INamedTypeSymbol? defaultDllImportSearchPathsAttributeTypeSymbol);
                if (!dllImportSearchDirectoryTypeIsPresent)
                    return;

                var dllImportTypeIsPresent = wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemRuntimeInteropServicesDllImportAttribute,
                    out INamedTypeSymbol? dllImportAttributeTypeSymbol);
                var libraryImportTypeIsPresent = wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemRuntimeInteropServicesLibraryImportAttribute,
                    out INamedTypeSymbol? libraryImportAttributeTypeSymbol);

                if ((!dllImportTypeIsPresent && !libraryImportTypeIsPresent)
                    || compilationStartAnalysisContext.Compilation.SyntaxTrees.FirstOrDefault() is not SyntaxTree tree)
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
                var defaultDllImportSearchPathsAttributeOnAssembly = compilation.Assembly.GetAttribute(defaultDllImportSearchPathsAttributeTypeSymbol);

                // Does not analyze local functions. To analyze local functions, we'll need to use RegisterSyntaxAction.
                compilationStartAnalysisContext.RegisterSymbolAction(symbolAnalysisContext =>
                {
                    var symbol = (IMethodSymbol)symbolAnalysisContext.Symbol;

                    if (!(symbol.IsStatic
                        // DllImport will always be extern. LibraryImport might not be extern but will be partial.
                        && (symbol.IsExtern || symbol.PartialImplementationPart != null)
                        // We do not want to warn on the PartialImplementationPart of LibraryImports. This will also be null for non-partial DllImports.
                        && symbol.PartialDefinitionPart == null))
                    {
                        return;
                    }

                    var dllImportAttribute = symbol.GetAttribute(dllImportAttributeTypeSymbol);
                    var libraryImportAttribute = symbol.GetAttribute(libraryImportAttributeTypeSymbol);
                    var defaultDllImportSearchPathsAttribute = symbol.GetAttribute(defaultDllImportSearchPathsAttributeTypeSymbol);

                    if (dllImportAttribute != null || libraryImportAttribute != null)
                    {
                        AttributeData primaryAttribute = libraryImportAttribute ?? dllImportAttribute!;
                        var constructorArguments = primaryAttribute.ConstructorArguments;

                        if (constructorArguments.IsEmpty ||
                            constructorArguments[0].Value is not { } value ||
                            Path.IsPathRooted(value.ToString()))
                        {
                            return;
                        }

                        var rule = UseDefaultDllImportSearchPathsAttributeRule;
                        var ruleArgument = symbol.Name;
                        var validatedDefaultDllImportSearchPathsAttribute = defaultDllImportSearchPathsAttribute ?? defaultDllImportSearchPathsAttributeOnAssembly;

                        if (validatedDefaultDllImportSearchPathsAttribute != null &&
                            validatedDefaultDllImportSearchPathsAttribute.ConstructorArguments.FirstOrDefault().Value is int dllImportSearchPath)
                        {
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
