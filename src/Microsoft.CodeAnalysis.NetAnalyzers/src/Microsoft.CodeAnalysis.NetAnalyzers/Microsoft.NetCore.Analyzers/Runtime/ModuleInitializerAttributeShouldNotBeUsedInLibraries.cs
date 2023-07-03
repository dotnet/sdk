// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2255: <inheritdoc cref="ModuleInitializerAttributeShouldNotBeUsedInLibrariesTitle"/>
    /// </summary>
    /// <remarks>
    /// ModuleInitializer methods must:
    /// - Be parameterless
    /// - Be void or async void
    /// - Not be generic or contained in a generic type
    /// - Be accessible in the module using public or internal
    /// </remarks>
#pragma warning disable RS1004 // The ModuleInitializer attribute feature only applies to C#
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004
    public sealed class ModuleInitializerAttributeShouldNotBeUsedInLibraries : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2255";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                    CreateLocalizableResourceString(nameof(ModuleInitializerAttributeShouldNotBeUsedInLibrariesTitle)),
                                                                    CreateLocalizableResourceString(nameof(ModuleInitializerAttributeShouldNotBeUsedInLibrariesMessage)),
                                                                    DiagnosticCategory.Usage,
                                                                    RuleLevel.BuildWarning,
                                                                    CreateLocalizableResourceString(nameof(ModuleInitializerAttributeShouldNotBeUsedInLibrariesDescription)),
                                                                    isPortedFxCopRule: false,
                                                                    isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesModuleInitializerAttribute, out var moduleInitializerAttribute))
                {
                    return;
                }

                // Only validate libraries (which will still produce some false positives, but that is acceptable)
                if (context.Compilation.Options.OutputKind != OutputKind.DynamicallyLinkedLibrary)
                    return;

                context.RegisterSymbolAction(context =>
                {
                    if (context.Symbol is IMethodSymbol method)
                    {
                        // Eliminate methods that would fail the CS8814, CS8815, and CS8816 checks
                        // for what can have [ModuleInitializer] applied
                        if (method.GetResultantVisibility() == SymbolVisibility.Private ||
                            method.Parameters.Length > 0 ||
                            method.IsGenericMethod ||
                            method.ContainingType.IsGenericType ||
                            !method.IsStatic ||
                            !method.ReturnsVoid)
                        {
                            return;
                        }

                        AttributeData? initializerAttribute = context.Symbol.GetAttribute(moduleInitializerAttribute);
                        SyntaxReference? attributeReference = initializerAttribute?.ApplicationSyntaxReference;

                        if (attributeReference is not null)
                        {
                            context.ReportDiagnostic(attributeReference.GetSyntax(context.CancellationToken).CreateDiagnostic(Rule));
                        }
                    }
                },
                SymbolKind.Method);
            });
        }
    }
}
