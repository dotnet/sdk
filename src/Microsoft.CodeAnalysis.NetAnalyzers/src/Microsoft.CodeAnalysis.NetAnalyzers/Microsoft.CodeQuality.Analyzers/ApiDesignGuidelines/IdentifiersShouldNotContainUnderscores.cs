// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1707: Identifiers should not contain underscores
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class IdentifiersShouldNotContainUnderscoresAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1707";
        private const string Uri = "https://docs.microsoft.com/visualstudio/code-quality/ca1707-identifiers-should-not-contain-underscores";

        private static readonly IImmutableSet<string> s_GlobalAsaxSpecialMethodNames =
            ImmutableHashSet.Create(
                "Application_AuthenticateRequest",
                "Application_BeginRequest",
                "Application_End",
                "Application_EndRequest",
                "Application_Error",
                "Application_Init",
                "Application_Start",
                "Session_End",
                "Session_Start");

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageAssembly = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageAssembly), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageNamespace = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageNamespace), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageType = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageType), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageMember = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageMember), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageTypeTypeParameter = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageTypeTypeParameter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageMethodTypeParameter = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageMethodTypeParameter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageMemberParameter = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageMemberParameter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageDelegateParameter = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresMessageDelegateParameter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor AssemblyRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageAssembly,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor NamespaceRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageNamespace,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor TypeRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageType,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor MemberRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageMember,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor TypeTypeParameterRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageTypeTypeParameter,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor MethodTypeParameterRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageMethodTypeParameter,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor MemberParameterRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageMemberParameter,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        internal static DiagnosticDescriptor DelegateParameterRule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDelegateParameter,
                                                                             DiagnosticCategory.Naming,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: Uri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AssemblyRule, NamespaceRule, TypeRule, MemberRule, TypeTypeParameterRule, MethodTypeParameterRule, MemberParameterRule, DelegateParameterRule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterSymbolAction(symbolAnalysisContext =>
            {
                var symbol = symbolAnalysisContext.Symbol;

                // FxCop compat: only analyze externally visible symbols by default
                // Note all the descriptors/rules for this analyzer have the same ID and category and hence
                // will always have identical configured visibility.
                if (!symbol.MatchesConfiguredVisibility(symbolAnalysisContext.Options, AssemblyRule, symbolAnalysisContext.CancellationToken))
                {
                    return;
                }

                switch (symbol.Kind)
                {
                    case SymbolKind.Namespace:
                        {
                            if (ContainsUnderScore(symbol.Name))
                            {
                                symbolAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(NamespaceRule, symbol.ToDisplayString()));
                            }

                            return;
                        }

                    case SymbolKind.NamedType:
                        {
                            var namedType = (INamedTypeSymbol)symbol;
                            AnalyzeTypeParameters(symbolAnalysisContext, namedType.TypeParameters);

                            if (namedType.TypeKind == TypeKind.Delegate &&
                                namedType.DelegateInvokeMethod != null)
                            {
                                AnalyzeParameters(symbolAnalysisContext, namedType.DelegateInvokeMethod.Parameters);
                            }

                            if (!ContainsUnderScore(symbol.Name))
                            {
                                return;
                            }

                            symbolAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(TypeRule, symbol.ToDisplayString()));
                            return;
                        }

                    case SymbolKind.Field:
                        {
                            var fieldSymbol = (IFieldSymbol)symbol;
                            if (ContainsUnderScore(symbol.Name) && (fieldSymbol.IsConst || (fieldSymbol.IsStatic && fieldSymbol.IsReadOnly)))
                            {
                                symbolAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(MemberRule, symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                                return;
                            }

                            return;
                        }

                    default:
                        {
                            if (symbol is IMethodSymbol methodSymbol)
                            {
                                if (methodSymbol.IsOperator())
                                {
                                    // Do not flag for operators.
                                    return;
                                }

                                if (methodSymbol.MethodKind == MethodKind.Conversion)
                                {
                                    // Do not flag for conversion methods generated for operators.
                                    return;
                                }

                                AnalyzeParameters(symbolAnalysisContext, methodSymbol.Parameters);
                                AnalyzeTypeParameters(symbolAnalysisContext, methodSymbol.TypeParameters);

                                if (s_GlobalAsaxSpecialMethodNames.Contains(methodSymbol.Name) &&
                                    methodSymbol.ContainingType.Inherits(symbolAnalysisContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebHttpApplication)))
                                {
                                    // Do not flag the convention based web methods.
                                    return;
                                }
                            }

                            if (symbol is IPropertySymbol propertySymbol)
                            {
                                AnalyzeParameters(symbolAnalysisContext, propertySymbol.Parameters);
                            }

                            if (!ContainsUnderScore(symbol.Name) || IsInvalidSymbol(symbol, symbolAnalysisContext))
                            {
                                return;
                            }

                            symbolAnalysisContext.ReportDiagnostic(symbol.CreateDiagnostic(MemberRule, symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                            return;
                        }
                }
            },
            SymbolKind.Namespace, // Namespace
            SymbolKind.NamedType, //Type
            SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event // Members
            );

            analysisContext.RegisterCompilationAction(compilationAnalysisContext =>
            {
                var compilation = compilationAnalysisContext.Compilation;
                if (ContainsUnderScore(compilation.AssemblyName))
                {
                    compilationAnalysisContext.ReportDiagnostic(compilation.Assembly.CreateDiagnostic(AssemblyRule, compilation.AssemblyName));
                }
            });
        }

        private static bool IsInvalidSymbol(ISymbol symbol, SymbolAnalysisContext context)
        {
            // Note all the descriptors/rules for this analyzer have the same ID and category and hence
            // will always have identical configured visibility.
            var matchesConfiguration = symbol.MatchesConfiguredVisibility(context.Options, AssemblyRule, context.CancellationToken);

            return (!(matchesConfiguration && !symbol.IsOverride)) ||
                symbol.IsAccessorMethod() || symbol.IsImplementationOfAnyInterfaceMember();
        }

        private static void AnalyzeParameters(SymbolAnalysisContext symbolAnalysisContext, IEnumerable<IParameterSymbol> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (ContainsUnderScore(parameter.Name) && !parameter.IsSymbolWithSpecialDiscardName())
                {
                    var containingType = parameter.ContainingType;

                    // Parameter in Delegate
                    if (containingType.TypeKind == TypeKind.Delegate)
                    {
                        if (containingType.IsPublic())
                        {
                            symbolAnalysisContext.ReportDiagnostic(parameter.CreateDiagnostic(DelegateParameterRule, containingType.ToDisplayString(), parameter.Name));
                        }
                    }
                    else if (!IsInvalidSymbol(parameter.ContainingSymbol, symbolAnalysisContext))
                    {
                        symbolAnalysisContext.ReportDiagnostic(parameter.CreateDiagnostic(MemberParameterRule, parameter.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), parameter.Name));
                    }
                }
            }
        }

        private static void AnalyzeTypeParameters(SymbolAnalysisContext symbolAnalysisContext, IEnumerable<ITypeParameterSymbol> typeParameters)
        {
            foreach (var typeParameter in typeParameters)
            {
                if (ContainsUnderScore(typeParameter.Name))
                {
                    var containingSymbol = typeParameter.ContainingSymbol;
                    if (containingSymbol.Kind == SymbolKind.NamedType)
                    {
                        if (containingSymbol.IsPublic())
                        {
                            symbolAnalysisContext.ReportDiagnostic(typeParameter.CreateDiagnostic(TypeTypeParameterRule, containingSymbol.ToDisplayString(), typeParameter.Name));
                        }
                    }
                    else if (containingSymbol.Kind == SymbolKind.Method && !IsInvalidSymbol(containingSymbol, symbolAnalysisContext))
                    {
                        symbolAnalysisContext.ReportDiagnostic(typeParameter.CreateDiagnostic(MethodTypeParameterRule, containingSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), typeParameter.Name));
                    }
                }
            }
        }

        private static bool ContainsUnderScore(string identifier)
        {
            return identifier.IndexOf('_') != -1;
        }
    }
}