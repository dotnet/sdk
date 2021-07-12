// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
        private static readonly LocalizableString s_staticAbstractMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.StaticAndAbstractRequiresPreviewFeatures), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_implementsEmptyPreviewInterface = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementsEmptyPreviewInterfaceMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
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

        internal static DiagnosticDescriptor StaticAbstractIsPreviewFeatureRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                    s_localizableTitle,
                                                                                                                    s_staticAbstractMessage,
                                                                                                                    DiagnosticCategory.Usage,
                                                                                                                    RuleLevel.IdeSuggestion,
                                                                                                                    s_localizableDescription,
                                                                                                                    isPortedFxCopRule: false,
                                                                                                                    isDataflowRule: false);
        internal static DiagnosticDescriptor ImplementsEmptyPreviewInterfaceRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                    s_localizableTitle,
                                                                                                                    s_implementsEmptyPreviewInterface,
                                                                                                                    DiagnosticCategory.Usage,
                                                                                                                    RuleLevel.IdeSuggestion,
                                                                                                                    s_localizableDescription,
                                                                                                                    isPortedFxCopRule: false,
                                                                                                                    isDataflowRule: false);

        private const string RequiresPreviewFeaturesAttribute = nameof(RequiresPreviewFeaturesAttribute);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GeneralPreviewFeatureAttributeRule, StaticAbstractIsPreviewFeatureRule, ImplementsEmptyPreviewInterfaceRule);

        private enum PreviewFeatureUsageType
        {
            General = 0,
            StaticAbstract,
            EmptyInterface
        }

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
                ConcurrentDictionary<ISymbol, PreviewFeatureUsageType> requiresPreviewFeaturesSymbolsToUsageType = new();

                if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesRuntimeFeatureVirtualStaticsInInterfaces, out INamedTypeSymbol? runtimeFeatureTypeVirtualStaticsInInterfaces))
                {
                    ProcessPreviewAttribute(runtimeFeatureTypeVirtualStaticsInInterfaces, requiresPreviewFeaturesSymbols);
                }

                // Handle user side invocation/references to preview features
                context.RegisterOperationAction(context => BuildSymbolInformationFromOperations(context, requiresPreviewFeaturesSymbols),
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation,
                    OperationKind.PropertyReference,
                    OperationKind.FieldReference,
                    OperationKind.DelegateCreation,
                    OperationKind.EventReference,
                    OperationKind.Unary,
                    OperationKind.Binary,
                    OperationKind.ArrayCreation,
                    OperationKind.CatchClause
                    );

                // Handle library side definitions of preview features
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, requiresPreviewFeaturesSymbols, requiresPreviewFeaturesSymbolsToUsageType, runtimeFeatureTypeVirtualStaticsInInterfaces), s_symbols);
            });
        }

        private static bool ProcessTypeSymbolAttributes(ITypeSymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, ConcurrentDictionary<ISymbol, PreviewFeatureUsageType> requiresPreviewFeaturesSymbolsToUsageType)
        {
            ImmutableArray<INamedTypeSymbol> interfaces = symbol.Interfaces;
            foreach (INamedTypeSymbol anInterface in interfaces)
            {
                var interfaceMembers = anInterface.GetMembers();
                if (interfaceMembers.Length == 0)
                {
                    // Only tag empty interfaces to prevent breaking changes in the future
                    requiresPreviewFeaturesSymbolsToUsageType.GetOrAdd(symbol, PreviewFeatureUsageType.EmptyInterface);
                    return true;
                }
            }

            INamedTypeSymbol? baseType = symbol.BaseType;
            if (baseType != null)
            {
                return ProcessPreviewAttribute(baseType, requiresPreviewFeaturesSymbols);
            }

            return false;
        }

        private static bool ProcessContainingTypePreviewAttributes(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols)
        {
            INamedTypeSymbol? containingType = symbol.ContainingType;
            // Namespaces do not have attributes
            while (containingType is INamespaceSymbol)
            {
                containingType = containingType.ContainingType;
            }

            if (containingType != null)
            {
                return ProcessPreviewAttribute(containingType, requiresPreviewFeaturesSymbols);
            }

            return false;
        }

        private static bool SymbolIsStaticAndAbstract(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols)
        {
            return symbol.IsStatic && symbol.IsAbstract && !ProcessContainingTypePreviewAttributes(symbol, requiresPreviewFeaturesSymbols);
        }

        private static bool ProcessPropertyOrMethodAttributes(ISymbol propertyOrMethodSymbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, ConcurrentDictionary<ISymbol, PreviewFeatureUsageType> requiresPreviewFeaturesSymbolsToUsageType, INamedTypeSymbol? runtimeFeatureType)
        {
            if (SymbolIsStaticAndAbstract(propertyOrMethodSymbol, requiresPreviewFeaturesSymbols))
            {
                // TODO: This needs to be tagged only on interfaces. A static abstract method on an abstract class is illegal and we shouldn't tag such cases.
                if (runtimeFeatureType != null && ProcessPreviewAttribute(runtimeFeatureType, requiresPreviewFeaturesSymbols))
                {
                    requiresPreviewFeaturesSymbolsToUsageType.GetOrAdd(propertyOrMethodSymbol, PreviewFeatureUsageType.StaticAbstract);
                    return true;
                }
            }

            if (propertyOrMethodSymbol.IsImplementationOfAnyImplicitInterfaceMember(out ISymbol baseInterfaceMember))
            {
                if (ProcessPreviewAttribute(baseInterfaceMember, requiresPreviewFeaturesSymbols) || ProcessContainingTypePreviewAttributes(baseInterfaceMember, requiresPreviewFeaturesSymbols))
                {
                    return true;
                }
            }

            if (propertyOrMethodSymbol.IsOverride)
            {
                ISymbol? overridden = propertyOrMethodSymbol.GetOverriddenMember();
                if (overridden != null && ProcessPreviewAttribute(overridden, requiresPreviewFeaturesSymbols))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, ConcurrentDictionary<ISymbol, PreviewFeatureUsageType> requiresPreviewFeaturesSymbolsToUsageType, INamedTypeSymbol? runtimeFeatureType)
        {
            ISymbol symbol = context.Symbol;

            // Don't report diagnostics on symbols that are marked as Preview
            if (ProcessPreviewAttribute(symbol, requiresPreviewFeaturesSymbols))
            {
                return;
            }

            var reportDiagnostic = symbol switch
            {
                ITypeSymbol typeSymbol => ProcessTypeSymbolAttributes(typeSymbol, requiresPreviewFeaturesSymbols, requiresPreviewFeaturesSymbolsToUsageType),
                IPropertySymbol or IMethodSymbol => ProcessPropertyOrMethodAttributes(symbol, requiresPreviewFeaturesSymbols, requiresPreviewFeaturesSymbolsToUsageType, runtimeFeatureType),
                _ => false
            };

            if (reportDiagnostic)
            {
                if (requiresPreviewFeaturesSymbolsToUsageType.TryGetValue(symbol, out PreviewFeatureUsageType usageType))
                {
                    if (usageType == PreviewFeatureUsageType.StaticAbstract)
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(StaticAbstractIsPreviewFeatureRule));
                    }
                    else if (usageType == PreviewFeatureUsageType.EmptyInterface)
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(ImplementsEmptyPreviewInterfaceRule, symbol.Name));
                    }
                }
                else
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(GeneralPreviewFeatureAttributeRule, symbol.Name));
                }
            }
        }

        private static void BuildSymbolInformationFromOperations(OperationAnalysisContext context, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols)
        {
            if (OperationUsesPreviewFeatures(context, requiresPreviewFeaturesSymbols, out ISymbol? symbol))
            {
                IOperation operation = context.Operation;
                if (operation is ICatchClauseOperation catchClauseOperation)
                {
                    operation = catchClauseOperation.ExceptionDeclarationOrExpression;
                }

                context.ReportDiagnostic(operation.CreateDiagnostic(GeneralPreviewFeatureAttributeRule, symbol.Name));
            }
        }

        private static bool OperationUsesPreviewFeatures(OperationAnalysisContext context, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, [NotNullWhen(true)] out ISymbol? symbol)
        {
            IOperation operation = context.Operation;
            ISymbol containingSymbol = context.ContainingSymbol;
            if (OperationUsesPreviewFeatures(containingSymbol, requiresPreviewFeaturesSymbols) || OperationUsesPreviewFeatures(containingSymbol.ContainingSymbol, requiresPreviewFeaturesSymbols))
            {
                symbol = null;
                return false;
            }

            symbol = GetOperationSymbol(operation);
            return symbol != null && OperationUsesPreviewFeatures(symbol, requiresPreviewFeaturesSymbols);
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
                _ => null,
            };

        private static bool OperationUsesPreviewFeatures(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols)
        {
            return ProcessContainingTypePreviewAttributes(symbol, requiresPreviewFeaturesSymbols) || ProcessPreviewAttribute(symbol, requiresPreviewFeaturesSymbols);
        }

        private static bool ProcessPreviewAttribute(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols)
        {
            if (!requiresPreviewFeaturesSymbols.TryGetValue(symbol, out bool existing))
            {
                bool ret = false;
                // TODO: Use symbol.HasAttribute(RequiresPreviewFeaturesAttribute) once we consume the latest sdk
                ImmutableArray<AttributeData> attributes = symbol.GetAttributes();
                foreach (AttributeData attribute in attributes)
                {
                    string attributeName = attribute.AttributeClass.Name;
                    if (attributeName == RequiresPreviewFeaturesAttribute)
                    {
                        ret = true;
                        break;
                    }
                }
                requiresPreviewFeaturesSymbols.GetOrAdd(symbol, ret);
                return ret;
            }

            return existing;
        }
    }
}
