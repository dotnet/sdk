// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1822: Mark members as static
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MarkMembersAsStaticAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1822";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkMembersAsStaticTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkMembersAsStaticMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MarkMembersAsStaticDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.IdeSuggestion,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();

            // Don't report in generated code since that's not actionable.
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(compilationContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);

                // Get the list of all method' attributes for which the rule shall not be triggered.
                ImmutableArray<INamedTypeSymbol> skippedAttributes = GetSkippedAttributes(wellKnownTypeProvider);

                compilationContext.RegisterSymbolStartAction(
                    symbolStartContext => OnSymbolStart(symbolStartContext, wellKnownTypeProvider, skippedAttributes),
                    SymbolKind.NamedType);
            });

            return;

            static void OnSymbolStart(
                SymbolStartAnalysisContext symbolStartContext,
                WellKnownTypeProvider wellKnownTypeProvider,
                ImmutableArray<INamedTypeSymbol> skippedAttributes)
            {
                // Since property/event accessors cannot be marked static themselves and the associated symbol (property/event)
                // has to be marked static, we want to report the diagnostic on the property/event.
                // So we make a note of the property/event symbols which have at least one accessor with no instance access.
                // At symbol end, we report candidate property/event symbols whose all accessors are candidates to be marked static.
                var propertyOrEventCandidates = PooledConcurrentSet<ISymbol>.GetInstance();
                var accessorCandidates = PooledConcurrentSet<IMethodSymbol>.GetInstance();

                var methodCandidates = PooledConcurrentSet<IMethodSymbol>.GetInstance();

                // Do not flag methods that are used as delegates: https://github.com/dotnet/roslyn-analyzers/issues/1511
                var methodsUsedAsDelegates = PooledConcurrentSet<IMethodSymbol>.GetInstance();

                symbolStartContext.RegisterOperationAction(OnMethodReference, OperationKind.MethodReference);
                symbolStartContext.RegisterOperationBlockStartAction(OnOperationBlockStart);
                symbolStartContext.RegisterSymbolEndAction(OnSymbolEnd);

                return;

                void OnMethodReference(OperationAnalysisContext operationContext)
                {
                    var methodReference = (IMethodReferenceOperation)operationContext.Operation;
                    methodsUsedAsDelegates.Add(methodReference.Method);
                }

                void OnOperationBlockStart(OperationBlockStartAnalysisContext blockStartContext)
                {
                    if (!(blockStartContext.OwningSymbol is IMethodSymbol methodSymbol))
                    {
                        return;
                    }

                    // Don't run any other check for this method if it isn't a valid analysis context
                    if (!ShouldAnalyze(methodSymbol, wellKnownTypeProvider, skippedAttributes))
                    {
                        return;
                    }

                    // Don't report methods which have a single throw statement
                    // with NotImplementedException or NotSupportedException
                    if (blockStartContext.IsMethodNotImplementedOrSupported())
                    {
                        return;
                    }

                    bool isInstanceReferenced = false;

                    blockStartContext.RegisterOperationAction(operationContext =>
                    {
                        if (((IInstanceReferenceOperation)operationContext.Operation).ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                        {
                            isInstanceReferenced = true;
                        }
                    }, OperationKind.InstanceReference);

                    blockStartContext.RegisterOperationBlockEndAction(blockEndContext =>
                    {
                        if (!isInstanceReferenced)
                        {
                            if (methodSymbol.IsAccessorMethod())
                            {
                                accessorCandidates.Add(methodSymbol);
                                propertyOrEventCandidates.Add(methodSymbol.AssociatedSymbol);
                            }
                            else if (methodSymbol.IsExternallyVisible())
                            {
                                blockEndContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name));
                            }
                            else
                            {
                                methodCandidates.Add(methodSymbol);
                            }
                        }
                    });
                }

                void OnSymbolEnd(SymbolAnalysisContext symbolEndContext)
                {
                    foreach (var candidate in methodCandidates)
                    {
                        if (methodsUsedAsDelegates.Contains(candidate))
                        {
                            continue;
                        }

                        symbolEndContext.ReportDiagnostic(candidate.CreateDiagnostic(Rule, candidate.Name));
                    }

                    foreach (var candidatePropertyOrEvent in propertyOrEventCandidates)
                    {
                        var allAccessorsAreCandidates = true;
                        foreach (var accessor in candidatePropertyOrEvent.GetAccessors())
                        {
                            if (!accessorCandidates.Contains(accessor))
                            {
                                allAccessorsAreCandidates = false;
                                break;
                            }
                        }

                        if (allAccessorsAreCandidates)
                        {
                            symbolEndContext.ReportDiagnostic(candidatePropertyOrEvent.CreateDiagnostic(Rule, candidatePropertyOrEvent.Name));
                        }
                    }

                    propertyOrEventCandidates.Free();
                    accessorCandidates.Free();
                    methodCandidates.Free();
                    methodsUsedAsDelegates.Free();
                }
            }
        }

        private static bool ShouldAnalyze(IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider, ImmutableArray<INamedTypeSymbol> skippedAttributes)
        {
            // Modifiers that we don't care about
            if (methodSymbol.IsStatic || methodSymbol.IsOverride || methodSymbol.IsVirtual ||
                methodSymbol.IsExtern || methodSymbol.IsAbstract || methodSymbol.IsImplementationOfAnyInterfaceMember())
            {
                return false;
            }

            if (IsSkippedMethod(methodSymbol, wellKnownTypeProvider))
            {
                return false;
            }

            // CA1000 says one shouldn't declare static members on generic types. So don't flag such cases.
            if (methodSymbol.ContainingType.IsGenericType && methodSymbol.IsExternallyVisible())
            {
                return false;
            }

            // FxCop doesn't check for the fully qualified name for these attributes - so we'll do the same.
            if (methodSymbol.GetAttributes().Any(attribute => skippedAttributes.Any(attr => attribute.AttributeClass.Inherits(attr))))
            {
                return false;
            }

            // If this looks like an event handler don't flag such cases.
            // However, we do want to consider EventRaise accessor as a candidate
            // so we can flag the associated event if none of it's accessors need instance reference.
            if (methodSymbol.Parameters.Length == 2 &&
                methodSymbol.Parameters[0].Type.SpecialType == SpecialType.System_Object &&
                IsEventArgs(methodSymbol.Parameters[1].Type, wellKnownTypeProvider) &&
                methodSymbol.MethodKind != MethodKind.EventRaise)
            {
                return false;
            }

            if (IsExplicitlyVisibleFromCom(methodSymbol, wellKnownTypeProvider))
            {
                return false;
            }

            return true;
        }

        private static bool IsEventArgs(ITypeSymbol type, WellKnownTypeProvider wellKnownTypeProvider)
        {
            if (type.DerivesFrom(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemEventArgs)))
            {
                return true;
            }

            if (type.IsValueType)
            {
                return type.Name.EndsWith("EventArgs", StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsExplicitlyVisibleFromCom(IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
        {
            if (!methodSymbol.IsExternallyVisible() || methodSymbol.IsGenericMethod)
            {
                return false;
            }

            var comVisibleAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesComVisibleAttribute);
            if (comVisibleAttribute == null)
            {
                return false;
            }

            if (methodSymbol.GetAttributes().Any(attribute => attribute.AttributeClass.Equals(comVisibleAttribute)) ||
                methodSymbol.ContainingType.GetAttributes().Any(attribute => attribute.AttributeClass.Equals(comVisibleAttribute)))
            {
                return true;
            }

            return false;
        }

        private static ImmutableArray<INamedTypeSymbol> GetSkippedAttributes(WellKnownTypeProvider wellKnownTypeProvider)
        {
            ImmutableArray<INamedTypeSymbol>.Builder? builder = null;

            void Add(INamedTypeSymbol? symbol)
            {
                if (symbol != null)
                {
                    builder ??= ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                    builder.Add(symbol);
                }
            }

            // Web attributes
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebServicesWebMethodAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpGetAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpPostAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpPutAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpDeleteAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpPatchAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpHeadAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebMvcHttpOptionsAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebHttpRouteAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpDeleteAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpGetAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPostAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPutAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpHeadAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpPatchAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcHttpOptionsAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftAspNetCoreMvcRouteAttribute));


            // MSTest attributes
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataTestMethodAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute));

            // XUnit attributes
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.XunitFactAttribute));

            // NUnit Attributes
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkSetUpAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkOneTimeSetUpAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkOneTimeTearDownAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkTestAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkTestCaseAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkTestCaseSourceAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkTheoryAttribute));
            Add(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.NUnitFrameworkTearDownAttribute));

            return builder?.ToImmutable() ?? ImmutableArray<INamedTypeSymbol>.Empty;
        }

        private static bool IsSkippedMethod(IMethodSymbol methodSymbol, WellKnownTypeProvider wellKnownTypeProvider)
        {
            if (methodSymbol.IsConstructor() || methodSymbol.IsFinalizer())
            {
                return true;
            }

            if (methodSymbol.ReturnsVoid &&
                methodSymbol.Parameters.IsEmpty &&
                (methodSymbol.Name == "Application_Start" || methodSymbol.Name == "Application_End") &&
                methodSymbol.ContainingType.Inherits(wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemWebHttpApplication)))
            {
                return true;
            }

            return false;
        }
    }
}
