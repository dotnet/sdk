// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1419: <inheritdoc cref="ProvidePublicParameterlessSafeHandleConstructorTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ProvidePublicParameterlessSafeHandleConstructorAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1419";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                                                        RuleId,
                                                        CreateLocalizableResourceString(nameof(ProvidePublicParameterlessSafeHandleConstructorTitle)),
                                                        CreateLocalizableResourceString(nameof(ProvidePublicParameterlessSafeHandleConstructorMessage)),
                                                        DiagnosticCategory.Interoperability,
                                                        RuleLevel.IdeSuggestion,
                                                        description: CreateLocalizableResourceString(nameof(ProvidePublicParameterlessSafeHandleConstructorDescription)),
                                                        isPortedFxCopRule: false,
                                                        isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(
                context =>
                {
                    if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesSafeHandle, out var safeHandleType))
                    {
                        context.RegisterSymbolAction(context => AnalyzeSymbol(context, safeHandleType), SymbolKind.NamedType);
                    }
                });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol safeHandleType)
        {
            INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;

            if (type.TypeKind != TypeKind.Class)
            {
                // SafeHandle-derived types can only be classes.
                return;
            }

            if (type.IsAbstract || !type.Inherits(safeHandleType))
            {
                // We only want to put the diagnostic on concrete SafeHandle-derived types.
                return;
            }

            foreach (var constructor in type.InstanceConstructors)
            {
                if (constructor.Parameters.Length == 0)
                {
                    if (constructor.GetResultantVisibility().IsAtLeastAsVisibleAs(type.GetResultantVisibility()))
                    {
                        // The parameterless constructor is as visible as the containing type, so there is no diagnostic to emit.
                        return;
                    }

                    context.ReportDiagnostic(constructor.CreateDiagnostic(Rule, type.Name));
                    break;
                }
            }
        }
    }
}
