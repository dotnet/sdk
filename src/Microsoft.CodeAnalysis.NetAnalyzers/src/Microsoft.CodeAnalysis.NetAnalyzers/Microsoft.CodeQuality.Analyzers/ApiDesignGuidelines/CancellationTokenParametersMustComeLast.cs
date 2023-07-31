// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1068: <inheritdoc cref="CancellationTokenParametersMustComeLastTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class CancellationTokenParametersMustComeLastAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1068";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(CancellationTokenParametersMustComeLastTitle)),
            CreateLocalizableResourceString(nameof(CancellationTokenParametersMustComeLastMessage)),
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);
                INamedTypeSymbol? cancellationTokenType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingCancellationToken);
                INamedTypeSymbol? iprogressType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIProgress1);

                var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
                builder.AddIfNotNull(compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesCallerFilePathAttribute));
                builder.AddIfNotNull(compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesCallerLineNumberAttribute));
                builder.AddIfNotNull(compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesCallerMemberNameAttribute));
                builder.AddIfNotNull(compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesCallerArgumentExpressionAttribute));
                var callerInformationAttributes = builder.ToImmutable();

                if (cancellationTokenType == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(symbolContext =>
                {
                    var methodSymbol = (IMethodSymbol)symbolContext.Symbol;

                    if (!methodSymbol.Parameters.Any(static (parameter, tokenType) => parameter.Type.Equals(tokenType), cancellationTokenType))
                    {
                        return;
                    }

                    if (methodSymbol.IsOverride ||
                        methodSymbol.IsImplementationOfAnyInterfaceMember())
                    {
                        return;
                    }

                    if (!symbolContext.Options.MatchesConfiguredVisibility(Rule, methodSymbol, symbolContext.Compilation,
                            defaultRequiredVisibility: SymbolVisibilityGroup.All))
                    {
                        return;
                    }

                    if (symbolContext.Options.IsConfiguredToSkipAnalysis(Rule, methodSymbol,
                            symbolContext.Compilation))
                    {
                        return;
                    }

                    int last = methodSymbol.Parameters.Length - 1;
                    if (last >= 0 && methodSymbol.Parameters[last].IsParams)
                    {
                        last--;
                    }

                    // Ignore parameters that have any of these attributes.
                    // C# reserved attributes: https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/caller-information
                    while (last >= 0
                        && methodSymbol.Parameters[last].HasAnyAttribute(callerInformationAttributes))
                    {
                        last--;
                    }

                    // Skip optional parameters, UNLESS one of them is a CancellationToken
                    // AND it's not the last one.
                    if (last >= 0 && methodSymbol.Parameters[last].IsOptional
                        && !SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[last].Type, cancellationTokenType))
                    {
                        last--;

                        while (last >= 0 && methodSymbol.Parameters[last].IsOptional)
                        {
                            if (SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[last].Type, cancellationTokenType))
                            {
                                symbolContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, methodSymbol.ToDisplayString()));
                            }

                            last--;
                        }
                    }

                    // Ignore multiple cancellation token parameters at the end of the parameter list.
                    while (last >= 0 && SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[last].Type, cancellationTokenType))
                    {
                        last--;
                    }

                    // Ignore parameters passed by reference when they appear at the end of the parameter list.
                    while (last >= 0 && methodSymbol.Parameters[last].RefKind != RefKind.None)
                    {
                        last--;
                    }

                    // Ignore IProgress<T> when last
                    if (last >= 0
                        && iprogressType != null
                        && SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[last].Type.OriginalDefinition, iprogressType))
                    {
                        last--;
                    }

                    for (int i = last - 1; i >= 0; i--)
                    {
                        ITypeSymbol parameterType = methodSymbol.Parameters[i].Type;
                        if (!SymbolEqualityComparer.Default.Equals(parameterType, cancellationTokenType))
                        {
                            continue;
                        }

                        // Bail if the CancellationToken is the first parameter of an extension method.
                        if (i == 0 && methodSymbol.IsExtensionMethod)
                        {
                            continue;
                        }

                        symbolContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, methodSymbol.ToDisplayString()));
                        break;
                    }
                },
                SymbolKind.Method);
            });
        }
    }
}
