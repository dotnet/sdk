// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1003: <inheritdoc cref="UseGenericEventHandlerInstancesTitle"/>
    ///
    /// Recommends that event handlers use <see cref="System.EventHandler{TEventArgs}"/>
    /// </summary>
    /// <remarks>
    /// NOTE: Legacy FxCop reports CA1009 for delegate type that handles a public or protected event and does not have the correct signature, return type, or parameter names.
    ///       This rule recommends fixing the signature to use a valid non-generic event handler.
    ///       We do not report CA1009, but instead report CA1003 and recommend using a generic event handler.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseGenericEventHandlerInstancesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1003";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(UseGenericEventHandlerInstancesTitle));

        internal static readonly DiagnosticDescriptor RuleForDelegates = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UseGenericEventHandlerInstancesForDelegateMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(UseGenericEventHandlerInstancesForDelegateDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleForEvents = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UseGenericEventHandlerInstancesForEventMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(UseGenericEventHandlerInstancesForEventDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleForEvents2 = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UseGenericEventHandlerInstancesForEvent2Message)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(UseGenericEventHandlerInstancesForEvent2Description)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(RuleForDelegates, RuleForEvents, RuleForEvents2);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                (context) =>
                {
                    INamedTypeSymbol? eventArgs = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs);
                    if (eventArgs == null)
                    {
                        return;
                    }

                    // Only analyze compilations that have a generic event handler defined.
                    if (context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventHandler1) == null)
                    {
                        return;
                    }

                    static bool IsDelegateTypeWithInvokeMethod(INamedTypeSymbol namedType) =>
                        namedType.TypeKind == TypeKind.Delegate && namedType.DelegateInvokeMethod != null;

                    context.RegisterSymbolAction(symbolContext =>
                    {
                        // Note all the descriptors/rules for this analyzer have the same ID and category and hence
                        // will always have identical configured visibility.
                        var namedType = (INamedTypeSymbol)symbolContext.Symbol;
                        if (symbolContext.Options.MatchesConfiguredVisibility(RuleForDelegates, namedType, symbolContext.Compilation) &&
                            IsDelegateTypeWithInvokeMethod(namedType) &&
                            namedType.DelegateInvokeMethod!.HasEventHandlerSignature(eventArgs))
                        {
                            // CA1003: Remove '{0}' and replace its usage with a generic EventHandler, for e.g. EventHandler&lt;T&gt;, where T is a valid EventArgs
                            symbolContext.ReportDiagnostic(namedType.CreateDiagnostic(RuleForDelegates, namedType.Name));
                        }
                    }, SymbolKind.NamedType);

                    INamedTypeSymbol? comSourceInterfacesAttribute = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesComSourceInterfacesAttribute);
                    bool ContainingTypeHasComSourceInterfacesAttribute(IEventSymbol eventSymbol) =>
                        comSourceInterfacesAttribute != null &&
                        eventSymbol.ContainingType.HasAnyAttribute(comSourceInterfacesAttribute);

                    context.RegisterSymbolAction(symbolContext =>
                    {
                        // NOTE: Legacy FxCop reports CA1009 for delegate type that handles a public or protected event and does not have the correct signature, return type, or parameter names.
                        //       which recommends fixing the signature to use a valid non-generic event handler.
                        //       We do not report CA1009, but instead report CA1003 and recommend using a generic event handler.
                        // Note all the descriptors/rules for this analyzer have the same ID and category and hence
                        // will always have identical configured visibility.
                        var eventSymbol = (IEventSymbol)symbolContext.Symbol;
                        if (symbolContext.Options.MatchesConfiguredVisibility(RuleForEvents, eventSymbol, symbolContext.Compilation) &&
                            !eventSymbol.IsOverride &&
                            !eventSymbol.IsImplementationOfAnyInterfaceMember() &&
                            !ContainingTypeHasComSourceInterfacesAttribute(eventSymbol) &&
                            eventSymbol.Type is INamedTypeSymbol eventType &&
                            IsDelegateTypeWithInvokeMethod(eventType))
                        {
                            if (eventType.IsImplicitlyDeclared)
                            {
                                // CA1003: Change the event '{0}' to use a generic EventHandler by defining the event type explicitly, for e.g. Event MyEvent As EventHandler(Of MyEventArgs).
                                symbolContext.ReportDiagnostic(eventSymbol.CreateDiagnostic(RuleForEvents2, eventSymbol.Name));
                            }
                            else if (!eventType.DelegateInvokeMethod!.HasEventHandlerSignature(eventArgs))
                            {
                                // CA1003: Change the event '{0}' to replace the type '{1}' with a generic EventHandler, for e.g. EventHandler&lt;T&gt;, where T is a valid EventArgs
                                symbolContext.ReportDiagnostic(eventSymbol.CreateDiagnostic(RuleForEvents, eventSymbol.Name, eventType.ToDisplayString()));
                            }
                        }
                    }, SymbolKind.Event);
                });
        }
    }
}
