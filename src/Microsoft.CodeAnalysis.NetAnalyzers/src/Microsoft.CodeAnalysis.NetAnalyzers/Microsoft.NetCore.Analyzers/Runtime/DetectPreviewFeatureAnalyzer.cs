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

        internal static DiagnosticDescriptor StaticAbstractIsPreviewFeatureRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                                                    s_localizableTitle,
                                                                                                                    s_staticAbstractMessage,
                                                                                                                    DiagnosticCategory.Usage,
                                                                                                                    RuleLevel.IdeSuggestion,
                                                                                                                    s_localizableDescription,
                                                                                                                    isPortedFxCopRule: false,
                                                                                                                    isDataflowRule: false);

        private const string RequiresPreviewFeaturesAttribute = nameof(RequiresPreviewFeaturesAttribute);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GeneralPreviewFeatureAttributeRule, StaticAbstractIsPreviewFeatureRule);

        private enum PreviewFeatureUsageType
        {
            General = 0,
            StaticAbstract,
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

                IFieldSymbol? virtualStaticsInInterfaces = null;
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesRuntimeFeature, out INamedTypeSymbol? runtimeFeatureType))
                {
                    virtualStaticsInInterfaces = runtimeFeatureType
                        .GetMembers("VirtualStaticsInInterfaces")
                        .OfType<IFieldSymbol>()
                        .FirstOrDefault();

                    if (virtualStaticsInInterfaces != null)
                    {
                        ProcessPreviewAttribute(virtualStaticsInInterfaces, requiresPreviewFeaturesSymbols, previewFeaturesAttribute);
                    }
                }

                // Handle user side invocation/references to preview features
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
                    OperationKind.TypeOf
                    );

                // Handle library side definitions of preview features
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, requiresPreviewFeaturesSymbols, requiresPreviewFeaturesSymbolsToUsageType, virtualStaticsInInterfaces, previewFeaturesAttribute), s_symbols);
            });
        }

        private static bool ProcessTypeSymbolAttributes(ITypeSymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            // We're only concerned about types(class/struct/interface) directly implementing preview interfaces. Implemented interfaces(direct/base) will report their diagnostics independently
            ImmutableArray<INamedTypeSymbol> interfaces = symbol.Interfaces;
            foreach (INamedTypeSymbol anInterface in interfaces)
            {
                if (ProcessPreviewAttribute(anInterface, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    return true;
                }
            }

            if (ProcessGenericTypesForPreviewAttributes(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                return true;
            }

            INamedTypeSymbol? baseType = symbol.BaseType;
            if (baseType != null)
            {
                return ProcessPreviewAttribute(baseType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
            }

            return false;
        }

        private static bool ProcessGenericTypesForPreviewAttributes(ISymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttribute)
        {
            if (symbol is INamedTypeSymbol typeSymbol && typeSymbol.Arity > 0)
            {
                ImmutableArray<ITypeSymbol> typeArguments = typeSymbol.TypeArguments;
                if (ProcessTypeArgumentsForPreviewAttributes(typeArguments, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                {
                    return true;
                }

                ImmutableArray<ITypeParameterSymbol> typeParameters = typeSymbol.TypeParameters;
                if (ProcessTypeParametersForPreviewAttributes(typeParameters, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                {
                    return true;
                }
            }

            if (symbol is IMethodSymbol methodSymbol && methodSymbol.Arity > 0)
            {
                ImmutableArray<ITypeSymbol> typeArguments = methodSymbol.TypeArguments;
                if (ProcessTypeArgumentsForPreviewAttributes(typeArguments, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                {
                    return true;
                }

                ImmutableArray<ITypeParameterSymbol> typeParameters = methodSymbol.TypeParameters;
                if (ProcessTypeParametersForPreviewAttributes(typeParameters, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ProcessContainingTypePreviewAttributes(ISymbol symbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            INamedTypeSymbol? containingType = symbol.ContainingType;
            // Namespaces do not have attributes
            while (containingType is INamespaceSymbol)
            {
                containingType = containingType.ContainingType;
            }

            if (containingType != null)
            {
                return ProcessPreviewAttribute(containingType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol) || ProcessGenericTypesForPreviewAttributes(containingType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
            }

            return false;
        }

        private static bool SymbolIsStaticAndAbstract(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            // Static Abstract is only legal on interfaces. Anything else is likely a compile error and we shouldn't tag such cases.
            return symbol.IsStatic && symbol.IsAbstract && symbol.ContainingType != null && symbol.ContainingType.TypeKind == TypeKind.Interface && !ProcessContainingTypePreviewAttributes(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol);
        }

        private static bool ProcessPropertyOrMethodAttributes(ISymbol propertyOrMethodSymbol,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            ConcurrentDictionary<ISymbol, PreviewFeatureUsageType> requiresPreviewFeaturesSymbolsToUsageType,
            IFieldSymbol? virtualStaticsInInterfaces,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            if (SymbolIsStaticAndAbstract(propertyOrMethodSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                if (virtualStaticsInInterfaces != null && ProcessPreviewAttribute(virtualStaticsInInterfaces, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    requiresPreviewFeaturesSymbolsToUsageType.GetOrAdd(propertyOrMethodSymbol, PreviewFeatureUsageType.StaticAbstract);
                    return true;
                }
            }

            if (propertyOrMethodSymbol.IsImplementationOfAnyImplicitInterfaceMember(out ISymbol baseInterfaceMember))
            {
                if (ProcessPreviewAttribute(baseInterfaceMember, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol) || ProcessContainingTypePreviewAttributes(baseInterfaceMember, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    return true;
                }
            }

            if (propertyOrMethodSymbol.IsOverride)
            {
                ISymbol? overridden = propertyOrMethodSymbol.GetOverriddenMember();
                if (overridden != null && ProcessPreviewAttribute(overridden, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    return true;
                }
            }

            if (propertyOrMethodSymbol is IMethodSymbol method)
            {
                if (ProcessPreviewAttribute(method.ReturnType, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                {
                    return true;
                }

                ImmutableArray<IParameterSymbol> parameters = method.Parameters;
                foreach (IParameterSymbol parameter in parameters)
                {
                    if (ProcessPreviewAttribute(parameter.Type, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol) ||
                        ProcessGenericTypesForPreviewAttributes(parameter.Type, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
                    {
                        return true;
                    }
                }
            }

            if (ProcessGenericTypesForPreviewAttributes(propertyOrMethodSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                return true;
            }

            return false;
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context,
            ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols,
            ConcurrentDictionary<ISymbol, PreviewFeatureUsageType> requiresPreviewFeaturesSymbolsToUsageType,
            IFieldSymbol? virtualStaticsInInterfaces,
            INamedTypeSymbol previewFeatureAttributeSymbol)
        {
            ISymbol symbol = context.Symbol;

            // Don't report diagnostics on symbols that are marked as Preview or if the ContainingType is Preview. We'll miss 2+ level nested type/method definitions, but that's ok. Only users who suppress the analyzer will run into this case.
            if (ProcessPreviewAttribute(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol) ||
                (symbol is IMethodSymbol method && method.AssociatedSymbol != null && ProcessPreviewAttribute(method.AssociatedSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol)))
            {
                return;
            }

            ISymbol containingSymbol = symbol.ContainingType;
            if (containingSymbol != null && ProcessPreviewAttribute(containingSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                return;
            }

            var reportDiagnostic = symbol switch
            {
                ITypeSymbol typeSymbol => ProcessTypeSymbolAttributes(typeSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol),
                IPropertySymbol or IMethodSymbol => ProcessPropertyOrMethodAttributes(symbol, requiresPreviewFeaturesSymbols, requiresPreviewFeaturesSymbolsToUsageType, virtualStaticsInInterfaces, previewFeatureAttributeSymbol),
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
                }
                else
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(GeneralPreviewFeatureAttributeRule, symbol.Name));
                }
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

        private static bool OperationUsesPreviewFeatures(OperationAnalysisContext context, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttributeSymbol, [NotNullWhen(true)] out ISymbol? symbol)
        {
            IOperation operation = context.Operation;
            ISymbol containingSymbol = context.ContainingSymbol;
            if (ProcessPreviewAttribute(containingSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol) ||
                ProcessPreviewAttribute(containingSymbol.ContainingSymbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol))
            {
                symbol = null;
                return false;
            }

            symbol = GetOperationSymbol(operation);
            return symbol != null && (ProcessPreviewAttribute(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol) || ProcessContainingTypePreviewAttributes(symbol, requiresPreviewFeaturesSymbols, previewFeatureAttributeSymbol));
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
                _ => null,
            };

        private static bool ProcessTypeParametersForPreviewAttributes(ImmutableArray<ITypeParameterSymbol> typeParameters, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttribute)
        {
            foreach (ITypeParameterSymbol typeParameter in typeParameters)
            {
                ImmutableArray<ITypeSymbol> constraintTypes = typeParameter.ConstraintTypes;
                foreach (ITypeSymbol constraintType in constraintTypes)
                {
                    if (ProcessPreviewAttribute(constraintType, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ProcessTypeArgumentsForPreviewAttributes(ImmutableArray<ITypeSymbol> typeArguments, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttribute)
        {
            foreach (ITypeSymbol typeParameter in typeArguments)
            {
                if (ProcessPreviewAttribute(typeParameter, requiresPreviewFeaturesSymbols, previewFeatureAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ProcessPreviewAttribute(ISymbol symbol, ConcurrentDictionary<ISymbol, bool> requiresPreviewFeaturesSymbols, INamedTypeSymbol previewFeatureAttribute)
        {
            if (!requiresPreviewFeaturesSymbols.TryGetValue(symbol, out bool existing))
            {
                if (symbol.HasAttribute(previewFeatureAttribute))
                {
                    requiresPreviewFeaturesSymbols.GetOrAdd(symbol, true);
                    return true;
                }

                requiresPreviewFeaturesSymbols.GetOrAdd(symbol, false);
                return false;
            }

            return existing;
        }
    }
}
