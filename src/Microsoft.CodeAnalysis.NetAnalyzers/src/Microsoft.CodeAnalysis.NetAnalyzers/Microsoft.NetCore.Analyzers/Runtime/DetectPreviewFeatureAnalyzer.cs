// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// Detect the use of [RequiresPreviewFeatures] in assemblies that have not opted into preview features
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DetectPreviewFeatureAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2252";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DetectPreviewFeaturesTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DetectPreviewFeaturesMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_implementsPreviewInterfaceMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementsPreviewInterfaceMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_implementsPreviewMethodMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementsPreviewMethodMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_overridesPreviewMethodMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.OverridesPreviewMethodMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_derivesFromPreviewClassMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DerivesFromPreviewClassMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_usesPreviewTypeParameterMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UsesPreviewTypeParameterMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_methodReturnsPreviewTypeMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MethodReturnsPreviewTypeMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_methodUsesPreviewTypeAsParameterMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MethodUsesPreviewTypeAsParamaterMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_fieldOfPreviewTypeMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.FieldIsPreviewTypeMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_staticAbstractMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.StaticAndAbstractRequiresPreviewFeatures), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DetectPreviewFeaturesDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly ImmutableArray<SymbolKind> s_symbols = ImmutableArray.Create(SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event);

        internal static DiagnosticDescriptor GeneralPreviewFeatureAttributeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                    s_localizableTitle,
                                                                                                                    s_localizableMessage,
                                                                                                                    DiagnosticCategory.Usage,
                                                                                                                    RuleLevel.IdeSuggestion,
                                                                                                                    s_localizableDescription,
                                                                                                                    isPortedFxCopRule: false,
                                                                                                                    isDataflowRule: false);

        internal static DiagnosticDescriptor ImplementsPreviewInterfaceRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                s_localizableTitle,
                                                                                                                s_implementsPreviewInterfaceMessage,
                                                                                                                DiagnosticCategory.Usage,
                                                                                                                RuleLevel.IdeSuggestion,
                                                                                                                s_localizableDescription,
                                                                                                                isPortedFxCopRule: false,
                                                                                                                isDataflowRule: false);

        internal static DiagnosticDescriptor ImplementsPreviewMethodRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                s_localizableTitle,
                                                                                                                s_implementsPreviewMethodMessage,
                                                                                                                DiagnosticCategory.Usage,
                                                                                                                RuleLevel.IdeSuggestion,
                                                                                                                s_localizableDescription,
                                                                                                                isPortedFxCopRule: false,
                                                                                                                isDataflowRule: false);

        internal static DiagnosticDescriptor OverridesPreviewMethodRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                s_localizableTitle,
                                                                                                                s_overridesPreviewMethodMessage,
                                                                                                                DiagnosticCategory.Usage,
                                                                                                                RuleLevel.IdeSuggestion,
                                                                                                                s_localizableDescription,
                                                                                                                isPortedFxCopRule: false,
                                                                                                                isDataflowRule: false);

        internal static DiagnosticDescriptor DerivesFromPreviewClassRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                             s_localizableTitle,
                                                                                                             s_derivesFromPreviewClassMessage,
                                                                                                             DiagnosticCategory.Usage,
                                                                                                             RuleLevel.IdeSuggestion,
                                                                                                             s_localizableDescription,
                                                                                                             isPortedFxCopRule: false,
                                                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor UsesPreviewTypeParameterRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                              s_localizableTitle,
                                                                                                              s_usesPreviewTypeParameterMessage,
                                                                                                              DiagnosticCategory.Usage,
                                                                                                              RuleLevel.IdeSuggestion,
                                                                                                              s_localizableDescription,
                                                                                                              isPortedFxCopRule: false,
                                                                                                              isDataflowRule: false);

        internal static DiagnosticDescriptor MethodReturnsPreviewTypeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                              s_localizableTitle,
                                                                                                              s_methodReturnsPreviewTypeMessage,
                                                                                                              DiagnosticCategory.Usage,
                                                                                                              RuleLevel.IdeSuggestion,
                                                                                                              s_localizableDescription,
                                                                                                              isPortedFxCopRule: false,
                                                                                                              isDataflowRule: false);

        internal static DiagnosticDescriptor MethodUsesPreviewTypeAsParameterRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                      s_localizableTitle,
                                                                                                                      s_methodUsesPreviewTypeAsParameterMessage,
                                                                                                                      DiagnosticCategory.Usage,
                                                                                                                      RuleLevel.IdeSuggestion,
                                                                                                                      s_localizableDescription,
                                                                                                                      isPortedFxCopRule: false,
                                                                                                                      isDataflowRule: false);
        internal static DiagnosticDescriptor FieldOrEventIsPreviewTypeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                        s_localizableTitle,
                                                                                                        s_fieldOfPreviewTypeMessage,
                                                                                                        DiagnosticCategory.Usage,
                                                                                                        RuleLevel.IdeSuggestion,
                                                                                                        s_localizableDescription,
                                                                                                        isPortedFxCopRule: false,
                                                                                                        isDataflowRule: false);

        internal static DiagnosticDescriptor StaticAbstractIsPreviewFeatureRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                    s_localizableTitle,
                                                                                                                    s_staticAbstractMessage,
                                                                                                                    DiagnosticCategory.Usage,
                                                                                                                    RuleLevel.IdeSuggestion,
                                                                                                                    s_localizableDescription,
                                                                                                                    isPortedFxCopRule: false,
                                                                                                                    isDataflowRule: false);

        private const string RequiresPreviewFeaturesAttribute = nameof(RequiresPreviewFeaturesAttribute);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GeneralPreviewFeatureAttributeRule,
            ImplementsPreviewInterfaceRule,
            ImplementsPreviewMethodRule,
            OverridesPreviewMethodRule,
            DerivesFromPreviewClassRule,
            UsesPreviewTypeParameterRule,
            MethodReturnsPreviewTypeRule,
            MethodUsesPreviewTypeAsParameterRule,
            FieldOrEventIsPreviewTypeRule,
            StaticAbstractIsPreviewFeatureRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeVersioningRequiresPreviewFeaturesAttribute, out var previewFeaturesAttribute))
                {
                    return;
                }

                if (context.Compilation.Assembly.HasAttribute(previewFeaturesAttribute))
                {
                    // This assembly has enabled preview attributes.
                    return;
                }

                ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols = new();

                IFieldSymbol? virtualStaticsInInterfaces = null;
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesRuntimeFeature, out INamedTypeSymbol? runtimeFeatureType))
                {
                    virtualStaticsInInterfaces = runtimeFeatureType
                        .GetMembers("VirtualStaticsInInterfaces")
                        .OfType<IFieldSymbol>()
                        .FirstOrDefault();

                    if (virtualStaticsInInterfaces != null)
                    {
                        SymbolIsAnnotatedAsPreview(virtualStaticsInInterfaces, requiresPreviewFeaturesSymbols, previewFeaturesAttribute);
                    }
                }

                // Handle symbol operations involving preview features
                context.RegisterOperationAction(context => BuildSymbolInformationFromOperations(context, requiresPreviewFeaturesSymbols, previewFeaturesAttribute),
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation,
                    OperationKind.PropertyReference,
                    OperationKind.FieldReference,
                    OperationKind.DelegateCreation,
                    OperationKind.EventReference,
                    OperationKind.Unary,
                    OperationKind.Binary,
                    OperationKind.ArrayCreation,
                    OperationKind.CatchClause,
                    OperationKind.TypeOf,
                    OperationKind.EventAssignment
                    );

                // Handle preview symbol definitions
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, requiresPreviewFeaturesSymbols, virtualStaticsInInterfaces, previewFeaturesAttribute), s_symbols);
            });
        }

        private static void ProcessFieldSymbolAttributes(SymbolAnalysisContext context, IFieldSymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            ISymbol symbolType = symbol.Type;
            while (symbolType is IArrayTypeSymbol arrayType)
            {
                symbolType = arrayType.ElementType;
            }

            ProcessFieldOrEventSymbolAttributes(context, symbol, symbolType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
        }

        private static void ProcessFieldOrEventSymbolAttributes(SymbolAnalysisContext context, ISymbol symbol, ISymbol symbolType,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            if (SymbolIsAnnotatedAsPreview(symbolType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                context.ReportDiagnostic(symbol.CreateDiagnostic(FieldOrEventIsPreviewTypeRule, symbol.Name, symbolType.Name));
            }
            if (SymbolContainsGenericTypesWithPreviewAttributes(symbolType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out ISymbol? previewSymbol))
            {
                context.ReportDiagnostic(symbol.CreateDiagnostic(FieldOrEventIsPreviewTypeRule, symbol.Name, previewSymbol.Name));
            }
        }

        private static void ProcessEventSymbolAttributes(SymbolAnalysisContext context, IEventSymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            ISymbol symbolType = symbol.Type;
            while (symbolType is IArrayTypeSymbol arrayType)
            {
                symbolType = arrayType.ElementType;
            }

            ProcessFieldOrEventSymbolAttributes(context, symbol, symbolType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
        }

        private static void ProcessTypeSymbolAttributes(SymbolAnalysisContext context, ITypeSymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            // We're only concerned about types(class/struct/interface) directly implementing preview interfaces. Implemented interfaces(direct/base) will report their diagnostics independently
            ImmutableArray<INamedTypeSymbol> interfaces = symbol.Interfaces;
            foreach (INamedTypeSymbol anInterface in interfaces)
            {
                if (SymbolIsAnnotatedAsPreview(anInterface, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(ImplementsPreviewInterfaceRule, symbol.Name, anInterface.Name));
                }
            }

            if (SymbolContainsGenericTypesWithPreviewAttributes(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out ISymbol? previewSymbol))
            {
                context.ReportDiagnostic(symbol.CreateDiagnostic(UsesPreviewTypeParameterRule, symbol.Name, previewSymbol.Name));
            }

            if (ProcessTypeAttributeForPreviewness(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out SyntaxReference? attributeSyntaxReference, out string? attributeName))
            {
                context.ReportDiagnostic(attributeSyntaxReference.GetSyntax(context.CancellationToken).CreateDiagnostic(GeneralPreviewFeatureAttributeRule, attributeName));
            }

            INamedTypeSymbol? baseType = symbol.BaseType;
            if (baseType != null)
            {
                if (SymbolIsAnnotatedAsPreview(baseType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(DerivesFromPreviewClassRule, symbol.Name, baseType.Name));
                }
            }
        }

        private static bool ProcessTypeAttributeForPreviewness(ISymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttribute, [NotNullWhen(true)] out SyntaxReference? attributeSyntaxReference, [NotNullWhen(true)] out string? attributeName)
        {
            ImmutableArray<AttributeData> attributes = symbol.GetAttributes();
            for (int i = 0; i < attributes.Length; i++)
            {
                AttributeData attribute = attributes[i];
                if (SymbolIsAnnotatedAsPreview(attribute.AttributeClass, requiresPreviewFeaturesSymbols, previewFeatureAttribute) || SymbolIsAnnotatedAsPreview(attribute.AttributeConstructor, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                {
                    attributeName = attribute.AttributeClass.Name;
                    attributeSyntaxReference = attribute.ApplicationSyntaxReference;
                    return true;
                }
            }

            attributeName = null;
            attributeSyntaxReference = null;
            return false;
        }

        private static bool SymbolContainsGenericTypesWithPreviewAttributes(ISymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttribute, [NotNullWhen(true)] out ISymbol? previewSymbol)
        {
            if (symbol is INamedTypeSymbol typeSymbol && typeSymbol.Arity > 0)
            {
                ImmutableArray<ITypeSymbol> typeArguments = typeSymbol.TypeArguments;
                if (TypeArgumentsHavePreviewAttribute(typeArguments, requiresPreviewFeaturesSymbols, previewFeatureAttribute, out previewSymbol))
                {
                    return true;
                }

                ImmutableArray<ITypeParameterSymbol> typeParameters = typeSymbol.TypeParameters;
                if (TypeParametersHavePreviewAttribute(typeParameters, requiresPreviewFeaturesSymbols, previewFeatureAttribute, out previewSymbol))
                {
                    return true;
                }
            }

            if (symbol is IMethodSymbol methodSymbol && methodSymbol.Arity > 0)
            {
                ImmutableArray<ITypeSymbol> typeArguments = methodSymbol.TypeArguments;
                if (TypeArgumentsHavePreviewAttribute(typeArguments, requiresPreviewFeaturesSymbols, previewFeatureAttribute, out previewSymbol))
                {
                    return true;
                }

                ImmutableArray<ITypeParameterSymbol> typeParameters = methodSymbol.TypeParameters;
                if (TypeParametersHavePreviewAttribute(typeParameters, requiresPreviewFeaturesSymbols, previewFeatureAttribute, out previewSymbol))
                {
                    return true;
                }
            }

            previewSymbol = null;
            return false;
        }

        private static bool SymbolIsStaticAndAbstract(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            // Static Abstract is only legal on interfaces. Anything else is likely a compile error and we shouldn't tag such cases.
            return symbol.IsStatic && symbol.IsAbstract && symbol.ContainingType != null && symbol.ContainingType.TypeKind == TypeKind.Interface && !SymbolIsAnnotatedAsPreview(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
        }

        private static void ProcessPropertyOrMethodAttributes(SymbolAnalysisContext context, ISymbol propertyOrMethodSymbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            IFieldSymbol? virtualStaticsInInterfaces,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            if (SymbolIsStaticAndAbstract(propertyOrMethodSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                if (virtualStaticsInInterfaces != null && SymbolIsAnnotatedAsPreview(virtualStaticsInInterfaces, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    context.ReportDiagnostic(propertyOrMethodSymbol.CreateDiagnostic(StaticAbstractIsPreviewFeatureRule));
                }
            }

            if (propertyOrMethodSymbol.IsImplementationOfAnyImplicitInterfaceMember(out ISymbol baseInterfaceMember))
            {
                if (SymbolIsAnnotatedAsPreview(baseInterfaceMember, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    string baseInterfaceMemberName = baseInterfaceMember.ContainingType != null ? baseInterfaceMember.ContainingType.Name + "." + baseInterfaceMember.Name : baseInterfaceMember.Name;
                    context.ReportDiagnostic(propertyOrMethodSymbol.CreateDiagnostic(ImplementsPreviewMethodRule, propertyOrMethodSymbol.Name, baseInterfaceMemberName));
                }
            }

            if (propertyOrMethodSymbol.IsOverride)
            {
                ISymbol overridden = propertyOrMethodSymbol.GetOverriddenMember();
                if (SymbolIsAnnotatedAsPreview(overridden, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    string overriddenName = overridden.ContainingType != null ? overridden.ContainingType.Name + "." + overridden.Name : overridden.Name;
                    context.ReportDiagnostic(propertyOrMethodSymbol.CreateDiagnostic(OverridesPreviewMethodRule, propertyOrMethodSymbol.Name, overriddenName));
                }
            }

            if (propertyOrMethodSymbol is IMethodSymbol method)
            {
                if (SymbolIsAnnotatedAsPreview(method.ReturnType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(MethodReturnsPreviewTypeRule, propertyOrMethodSymbol.Name, method.ReturnType.Name));
                }

                ImmutableArray<IParameterSymbol> parameters = method.Parameters;
                foreach (IParameterSymbol parameter in parameters)
                {
                    if (SymbolIsAnnotatedAsPreview(parameter.Type, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                    {
                        context.ReportDiagnostic(parameter.CreateDiagnostic(MethodUsesPreviewTypeAsParameterRule, propertyOrMethodSymbol.Name, parameter.Type.Name));
                    }

                    if (SymbolContainsGenericTypesWithPreviewAttributes(parameter.Type, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out ISymbol? referencedPreviewSymbol))
                    {
                        context.ReportDiagnostic(propertyOrMethodSymbol.CreateDiagnostic(UsesPreviewTypeParameterRule, propertyOrMethodSymbol.Name, referencedPreviewSymbol.Name));
                    }
                }
            }

            if (SymbolContainsGenericTypesWithPreviewAttributes(propertyOrMethodSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out ISymbol? previewSymbol))
            {
                context.ReportDiagnostic(propertyOrMethodSymbol.CreateDiagnostic(UsesPreviewTypeParameterRule, propertyOrMethodSymbol.Name, previewSymbol.Name));
            }
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            IFieldSymbol? virtualStaticsInInterfaces,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            ISymbol symbol = context.Symbol;

            if (SymbolIsAnnotatedAsPreview(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol) ||
                (symbol is IMethodSymbol method && method.AssociatedSymbol != null && SymbolIsAnnotatedAsPreview(method.AssociatedSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol)))
            {
                return;
            }

            if (symbol is ITypeSymbol typeSymbol)
            {
                ProcessTypeSymbolAttributes(context, typeSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
            }
            else if (symbol is IMethodSymbol || symbol is IPropertySymbol)
            {
                ProcessPropertyOrMethodAttributes(context, symbol, requiresPreviewFeaturesSymbols,/* requiresPreviewFeaturesSymbolsToUsageType,*/ virtualStaticsInInterfaces, previewFeatureAttributeSymbol);
            }
            else if (symbol is IFieldSymbol fieldSymbol)
            {
                ProcessFieldSymbolAttributes(context, fieldSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
            }
            else if (symbol is IEventSymbol eventSymbol)
            {
                ProcessEventSymbolAttributes(context, eventSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
            }
        }

        private static void BuildSymbolInformationFromOperations(OperationAnalysisContext context, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            if (OperationUsesPreviewFeatures(context, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out ISymbol? symbol))
            {
                IOperation operation = context.Operation;
                if (operation is ICatchClauseOperation catchClauseOperation)
                {
                    operation = catchClauseOperation.ExceptionDeclarationOrExpression;
                }

                context.ReportDiagnostic(operation.CreateDiagnostic(GeneralPreviewFeatureAttributeRule, symbol.Name));
            }
        }

        private static bool SymbolIsAnnotatedOrUsesPreviewTypes(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttributeSymbol, [NotNullWhen(true)] out ISymbol? referencedPreviewSymbol)
        {
            if (SymbolIsAnnotatedAsPreview(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                referencedPreviewSymbol = symbol;
                return true;
            }

            if (SymbolContainsGenericTypesWithPreviewAttributes(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out referencedPreviewSymbol))
            {
                return true;
            }

            referencedPreviewSymbol = null;
            return false;
        }

        private static bool OperationUsesPreviewFeatures(OperationAnalysisContext context, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttributeSymbol, [NotNullWhen(true)] out ISymbol? referencedPreviewSymbol)
        {
            IOperation operation = context.Operation;
            ISymbol containingSymbol = context.ContainingSymbol;
            if (SymbolIsAnnotatedAsPreview(containingSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                referencedPreviewSymbol = null;
                return false;
            }

            ISymbol? symbol = GetOperationSymbol(operation);
            if (symbol != null)
            {
                if (symbol is IPropertySymbol propertySymbol)
                {
                    // bool AProperty => true is different from bool AProperty { get => false }. Handle both here
                    if (SymbolIsAnnotatedOrUsesPreviewTypes(propertySymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out referencedPreviewSymbol))
                    {
                        return true;
                    }

                    ValueUsageInfo usageInfo = operation.GetValueUsageInfo(propertySymbol);
                    if (usageInfo.IsReadFrom())
                    {
                        symbol = propertySymbol.GetMethod;
                    }

                    if (usageInfo.IsWrittenTo() || usageInfo == ValueUsageInfo.ReadWrite)
                    {
                        symbol = propertySymbol.SetMethod;
                    }
                }

                if (operation is IEventAssignmentOperation eventAssignment && symbol is IEventSymbol eventSymbol)
                {
                    symbol = eventAssignment.Adds ? eventSymbol.AddMethod : eventSymbol.RemoveMethod;
                }

                if (symbol is IMethodSymbol methodSymbol && methodSymbol.IsConstructor())
                {
                    if (SymbolIsAnnotatedOrUsesPreviewTypes(methodSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out referencedPreviewSymbol))
                    {
                        // Constructor symbols have the name .ctor. Return the containing type instead so we get meaningful names in the diagnostic message
                        referencedPreviewSymbol = referencedPreviewSymbol.ContainingType;
                        return true;
                    }

                    if (SymbolIsAnnotatedOrUsesPreviewTypes(methodSymbol.ContainingType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out referencedPreviewSymbol))
                    {
                        return true;
                    }
                }

                if (SymbolIsAnnotatedOrUsesPreviewTypes(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol, out referencedPreviewSymbol))
                {
                    return true;
                }
            }

            referencedPreviewSymbol = null;
            return false;
        }

        private static ISymbol? GetOperationSymbol(IOperation operation)
            => operation switch
            {
                IInvocationOperation iOperation => iOperation.TargetMethod,
                IObjectCreationOperation cOperation => cOperation.Constructor,
                IPropertyReferenceOperation pOperation => pOperation.Property,
                IFieldReferenceOperation fOperation => fOperation.Field,
                IDelegateCreationOperation dOperation => dOperation.Type,
                IEventReferenceOperation eOperation => eOperation.Member,
                IUnaryOperation uOperation => uOperation.OperatorMethod,
                IBinaryOperation bOperation => bOperation.OperatorMethod,
                IArrayCreationOperation arrayCreationOperation => (arrayCreationOperation.Type as IArrayTypeSymbol)?.ElementType,
                ICatchClauseOperation catchClauseOperation => catchClauseOperation.ExceptionType,
                ITypeOfOperation typeOfOperation => typeOfOperation.TypeOperand,
                IEventAssignmentOperation eventAssignment => GetOperationSymbol(eventAssignment.EventReference),
                _ => null,
            };

        private static bool TypeParametersHavePreviewAttribute(ImmutableArray<ITypeParameterSymbol> typeParameters, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttribute, [NotNullWhen(true)] out ISymbol? previewSymbol)
        {
            foreach (ITypeParameterSymbol typeParameter in typeParameters)
            {
                ImmutableArray<ITypeSymbol> constraintTypes = typeParameter.ConstraintTypes;
                foreach (ITypeSymbol constraintType in constraintTypes)
                {
                    if (SymbolIsAnnotatedAsPreview(constraintType, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                    {
                        previewSymbol = constraintType;
                        return true;
                    }
                }
            }

            previewSymbol = null;
            return false;
        }

        private static bool TypeArgumentsHavePreviewAttribute(ImmutableArray<ITypeSymbol> typeArguments, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttribute, [NotNullWhen(true)] out ISymbol? previewSymbol)
        {
            foreach (ITypeSymbol typeParameter in typeArguments)
            {
                if (SymbolIsAnnotatedAsPreview(typeParameter, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                {
                    previewSymbol = typeParameter;
                    return true;
                }
            }

            previewSymbol = null;
            return false;
        }

        private static bool SymbolIsAnnotatedAsPreview(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttribute)
        {
            static bool ContainingSymbolHeirarchyIsAnnotatedAsPreview(ISymbol symbol,
                ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
                INamedTypeSymbol previewFeatureAttribute, [NotNullWhen(true)] out ISymbol? containingPreviewSymbol)
            {
                INamedTypeSymbol? containingType = symbol.ContainingType;
                while (containingType != null)
                {
                    // Namespaces do not have attributes
                    if (!(containingType is INamespaceSymbol))
                    {
                        if (containingType.HasAttribute(previewFeatureAttribute))
                        {
                            requiresPreviewFeaturesSymbols.GetOrAdd(containingType, true);
                            containingPreviewSymbol = containingType;
                            return true;
                        }
                    }

                    containingType = containingType.ContainingType;
                }

                containingPreviewSymbol = null;
                return false;
            }

            if (!requiresPreviewFeaturesSymbols.TryGetValue(symbol, out bool existing))
            {
                if (symbol.HasAttribute(previewFeatureAttribute))
                {
                    requiresPreviewFeaturesSymbols.GetOrAdd(symbol, true);
                    return true;
                }

                if (ContainingSymbolHeirarchyIsAnnotatedAsPreview(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttribute, out ISymbol? containingPreviewSymbol))
                {
                    ISymbol loopSymbol = symbol.ContainingSymbol;
                    requiresPreviewFeaturesSymbols.GetOrAdd(loopSymbol, true);
                    while (loopSymbol != containingPreviewSymbol)
                    {
                        requiresPreviewFeaturesSymbols.GetOrAdd(loopSymbol, true);
                        loopSymbol = loopSymbol.ContainingSymbol;
                    }

                    return true;
                }

                requiresPreviewFeaturesSymbols.GetOrAdd(symbol, false);
                return false;
            }

            return existing;
        }
    }
}
