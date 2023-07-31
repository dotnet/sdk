// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1812: <inheritdoc cref="AvoidUninstantiatedInternalClassesTitle"/>
    /// </summary>
    public abstract class AvoidUninstantiatedInternalClassesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1812";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidUninstantiatedInternalClassesTitle)),
            CreateLocalizableResourceString(nameof(AvoidUninstantiatedInternalClassesMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,    // Code coverage tools provide superior results when done correctly.
            description: CreateLocalizableResourceString(nameof(AvoidUninstantiatedInternalClassesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isReportedAtCompilationEnd: true);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public abstract void RegisterLanguageSpecificChecks(CompilationStartAnalysisContext context, ConcurrentDictionary<INamedTypeSymbol, object?> instantiatedTypes);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

            context.RegisterCompilationStartAction(startContext =>
            {
                ConcurrentDictionary<INamedTypeSymbol, object?> instantiatedTypes = new ConcurrentDictionary<INamedTypeSymbol, object?>();
                var internalTypes = new ConcurrentDictionary<INamedTypeSymbol, object?>();

                var compilation = startContext.Compilation;
                var entryPointContainingType = compilation.GetEntryPoint(startContext.CancellationToken)?.ContainingType;
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
                var hasInternalsVisibleTo = startContext.Compilation.Assembly.HasAnyAttribute(
                    startContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesInternalsVisibleToAttribute));

                var systemAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemAttribute);
                var iConfigurationSectionHandlerSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConfigurationIConfigurationSectionHandler);
                var configurationSectionSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConfigurationConfigurationSection);
                var safeHandleSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesSafeHandle);
                var traceListenerSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsTraceListener);
                var mef1ExportAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemComponentModelCompositionExportAttribute);
                var mef2ExportAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCompositionExportAttribute);

                var coClassAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesCoClassAttribute);
                var designerAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemComponentModelDesignerAttribute);
                var debuggerTypeProxyAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsDebuggerTypeProxyAttribute);

                var instantiatingAttributeChecker = new List<(Func<INamedTypeSymbol, bool> isAttributeTarget, Func<AttributeData, Compilation, INamedTypeSymbol?> findTypeOrDefault)>
                {
                    (type => CanBeCoClassAttributeContext(type), (attribute, _) => FindTypeIfCoClassAttribute(attribute)),
                    (type => CanBeDesignerAttributeContext(type), (attribute, compilation) => FindTypeIfDesignerAttribute(attribute, compilation)),
                    (type => CanBeDebuggerTypeProxyAttributeContext(type), (attribute, compilation) => FindTypeIfDebuggerTypeProxyAttribute(attribute, compilation)),
                };

                RegisterLanguageSpecificChecks(startContext, instantiatedTypes);

                startContext.RegisterOperationAction(context =>
                {
                    var expr = (IObjectCreationOperation)context.Operation;
                    if (expr.Type is INamedTypeSymbol namedType)
                    {
                        instantiatedTypes.TryAdd(namedType, null);
                    }
                }, OperationKind.ObjectCreation);

                startContext.RegisterSymbolAction(context =>
                {
                    var type = (INamedTypeSymbol)context.Symbol;
                    if (!type.IsExternallyVisible() &&
                        !IsOkToBeUninstantiated(type,
                            entryPointContainingType,
                            systemAttributeSymbol,
                            iConfigurationSectionHandlerSymbol,
                            configurationSectionSymbol,
                            safeHandleSymbol,
                            traceListenerSymbol,
                            mef1ExportAttributeSymbol,
                            mef2ExportAttributeSymbol))
                    {
                        internalTypes.TryAdd(type, null);
                    }

                    // Instantiation from the subtype constructor initializer.
                    if (type.BaseType != null)
                    {
                        instantiatedTypes.TryAdd(type.BaseType, null);
                    }

                    // Some attributes are known to behave as type activator so we want to check them
                    var applicableAttributes = instantiatingAttributeChecker.Where(tuple => tuple.isAttributeTarget(type)).ToArray();
                    foreach (var attribute in type.GetAttributes())
                    {
                        foreach (var (_, findTypeOrDefault) in applicableAttributes)
                        {
                            if (findTypeOrDefault(attribute, context.Compilation) is INamedTypeSymbol namedType)
                            {
                                instantiatedTypes.TryAdd(namedType, null);
                                break;
                            }
                        }
                    }
                }, SymbolKind.NamedType);

                startContext.RegisterOperationAction(context =>
                {
                    var expr = (IObjectCreationOperation)context.Operation;
                    var constructedClass = (INamedTypeSymbol?)expr.Type;

                    if (constructedClass == null || !constructedClass.IsGenericType || constructedClass.IsUnboundGenericType)
                    {
                        return;
                    }

                    var generics = constructedClass.TypeParameters.Zip(constructedClass.TypeArguments, (parameter, argument) => (parameter, argument));
                    ProcessGenericTypes(generics, instantiatedTypes);
                }, OperationKind.ObjectCreation);

                startContext.RegisterOperationAction(context =>
                {
                    var expr = (IInvocationOperation)context.Operation;
                    var methodType = expr.TargetMethod;

                    if (!methodType.IsGenericMethod)
                    {
                        return;
                    }

                    var generics = methodType.TypeParameters.Zip(methodType.TypeArguments, (parameter, argument) => (parameter, argument));
                    ProcessGenericTypes(generics, instantiatedTypes);
                }, OperationKind.Invocation);

                startContext.RegisterCompilationEndAction(context =>
                {
                    var uninstantiatedInternalTypes = internalTypes
                        .Select(it => it.Key.OriginalDefinition)
                        .Except(instantiatedTypes.Select(it => it.Key.OriginalDefinition))
                        .Where(type => !HasInstantiatedNestedType(type, instantiatedTypes.Keys));

                    foreach (var type in uninstantiatedInternalTypes)
                    {
                        if (!hasInternalsVisibleTo || context.Options.GetBoolOptionValue(EditorConfigOptionNames.IgnoreInternalsVisibleTo, Rule, type, context.Compilation, defaultValue: false))
                        {
                            context.ReportDiagnostic(type.CreateDiagnostic(Rule, type.FormatMemberName()));
                        }
                    }
                });

                return;

                // Local functions

                bool CanBeCoClassAttributeContext(INamedTypeSymbol type)
                     => coClassAttributeSymbol != null && type.TypeKind == TypeKind.Interface;

                INamedTypeSymbol? FindTypeIfCoClassAttribute(AttributeData attribute)
                {
                    RoslynDebug.Assert(coClassAttributeSymbol != null);

                    if (attribute.AttributeClass != null &&
                        attribute.AttributeClass.Equals(coClassAttributeSymbol) &&
                        attribute.ConstructorArguments.Length == 1 &&
                        attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type &&
                        attribute.ConstructorArguments[0].Value is INamedTypeSymbol typeSymbol &&
                        typeSymbol.TypeKind == TypeKind.Class)
                    {
                        return typeSymbol;
                    }

                    return null;
                }

                bool CanBeDesignerAttributeContext(INamedTypeSymbol type)
                    => designerAttributeSymbol != null && (type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.Class);

                INamedTypeSymbol? FindTypeIfDesignerAttribute(AttributeData attribute, Compilation compilation)
                {
                    RoslynDebug.Assert(designerAttributeSymbol != null);

                    if (attribute.ConstructorArguments.Length is not (1 or 2) ||
                        attribute.AttributeClass == null ||
                        !attribute.AttributeClass.Equals(designerAttributeSymbol))
                    {
                        return null;
                    }

                    switch (attribute.ConstructorArguments[0].Value)
                    {
                        case string designerTypeName:
                            {
                                if (IsTypeInCurrentAssembly(designerTypeName, compilation, out var namedType))
                                {
                                    return namedType;
                                }

                                break;
                            }

                        case INamedTypeSymbol namedType:
                            return namedType;
                    }

                    return null;
                }

                bool CanBeDebuggerTypeProxyAttributeContext(INamedTypeSymbol type)
                    => debuggerTypeProxyAttributeSymbol != null && (type.TypeKind == TypeKind.Struct || type.TypeKind == TypeKind.Class);

                INamedTypeSymbol? FindTypeIfDebuggerTypeProxyAttribute(AttributeData attribute, Compilation compilation)
                {
                    RoslynDebug.Assert(debuggerTypeProxyAttributeSymbol != null);

                    if (attribute.AttributeClass == null ||
                        !attribute.AttributeClass.Equals(debuggerTypeProxyAttributeSymbol))
                    {
                        return null;
                    }

                    switch (attribute.ConstructorArguments[0].Value)
                    {
                        case string typeName:
                            {
                                if (IsTypeInCurrentAssembly(typeName, compilation, out var namedType))
                                {
                                    return namedType;
                                }

                                break;
                            }

                        case INamedTypeSymbol namedType:
                            return namedType;
                    }

                    return null;
                }
            });
        }

        private bool HasInstantiatedNestedType(INamedTypeSymbol type, IEnumerable<INamedTypeSymbol> instantiatedTypes)
        {
            // We don't care whether a private nested type is instantiated, because if it
            // is, it can only have happened within the type itself.
            var nestedTypes = type.GetTypeMembers().Where(member => member.DeclaredAccessibility != Accessibility.Private);

            foreach (var nestedType in nestedTypes)
            {
                if (instantiatedTypes.Contains(nestedType))
                {
                    return true;
                }

                if (HasInstantiatedNestedType(nestedType, instantiatedTypes))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOkToBeUninstantiated(
            INamedTypeSymbol type,
            INamedTypeSymbol? entryPointContainingType,
            INamedTypeSymbol? systemAttributeSymbol,
            INamedTypeSymbol? iConfigurationSectionHandlerSymbol,
            INamedTypeSymbol? configurationSectionSymbol,
            INamedTypeSymbol? safeHandleSymbol,
            INamedTypeSymbol? traceListenerSymbol,
            INamedTypeSymbol? mef1ExportAttributeSymbol,
            INamedTypeSymbol? mef2ExportAttributeSymbol)
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic)
            {
                return true;
            }

            // Attributes are not instantiated in IL but are created by reflection.
            if (type.Inherits(systemAttributeSymbol))
            {
                return true;
            }

            // The type containing the assembly's entry point is OK.
            if (SymbolEqualityComparer.Default.Equals(entryPointContainingType, type))
            {
                return true;
            }

            // MEF exported classes are instantiated by MEF, by reflection.
            if (IsMefExported(type, mef1ExportAttributeSymbol, mef2ExportAttributeSymbol))
            {
                return true;
            }

            // Types implementing the (deprecated) IConfigurationSectionHandler interface
            // are OK because they are instantiated by the configuration system.
            if (type.Inherits(iConfigurationSectionHandlerSymbol))
            {
                return true;
            }

            // Likewise for types derived from ConfigurationSection.
            if (type.Inherits(configurationSectionSymbol))
            {
                return true;
            }

            // SafeHandles can be created from within the type itself by native code.
            if (type.Inherits(safeHandleSymbol))
            {
                return true;
            }

            if (type.Inherits(traceListenerSymbol))
            {
                return true;
            }

            if (type.IsStaticHolderType())
            {
                return true;
            }

            return false;
        }

        public static bool IsMefExported(
            INamedTypeSymbol type,
            INamedTypeSymbol? mef1ExportAttributeSymbol,
            INamedTypeSymbol? mef2ExportAttributeSymbol)
        {
            return (mef1ExportAttributeSymbol != null && type.HasAnyAttribute(mef1ExportAttributeSymbol))
                || (mef2ExportAttributeSymbol != null && type.HasAnyAttribute(mef2ExportAttributeSymbol));
        }

        /// <summary>
        /// If a type is passed a generic argument to another type or a method that specifies that the type must have a constructor,
        /// we presume that the method will be constructing the type, and add it to the list of instantiated types.
        /// </summary>
        protected void ProcessGenericTypes(IEnumerable<(ITypeParameterSymbol param, ITypeSymbol arg)> generics, ConcurrentDictionary<INamedTypeSymbol, object?> instantiatedTypes)
        {
            foreach (var (typeParam, typeArg) in generics)
            {
                if (typeParam.HasConstructorConstraint)
                {
                    void ProcessNamedTypeParamConstraint(INamedTypeSymbol namedTypeArg)
                    {
                        if (!instantiatedTypes.TryAdd(namedTypeArg, null))
                        {
                            // Already processed.
                            return;
                        }

                        // We need to handle if this type param also has type params that have a generic constraint. Take the following example:
                        // new Factory1<Factory2<InstantiatedType>>();
                        // In this example, Factory1 and Factory2 have type params with constructor constraints. Therefore, we need to add all 3
                        // types to the list of types that have actually been instantiated. However, in the following example:
                        // new List<Factory<InstantiatedType>>();
                        // List does not have a constructor constraint, so we can't reasonably infer anything about its type parameters.
                        if (namedTypeArg.IsGenericType)
                        {
                            var newGenerics = namedTypeArg.TypeParameters.Zip(namedTypeArg.TypeArguments, (parameter, argument) => (parameter, argument));
                            ProcessGenericTypes(newGenerics, instantiatedTypes);
                        }
                    }

                    if (typeArg is INamedTypeSymbol namedType)
                    {
                        ProcessNamedTypeParamConstraint(namedType);
                    }
                    else if (typeArg is ITypeParameterSymbol typeParameterArg && !typeParameterArg.ConstraintTypes.IsEmpty)
                    {
                        static IEnumerable<INamedTypeSymbol> GetAllNamedTypeConstraints(ITypeParameterSymbol t)
                        {
                            var directConstraints = t.ConstraintTypes.OfType<INamedTypeSymbol>();
                            var inheritedConstraints = t.ConstraintTypes.OfType<ITypeParameterSymbol>()
                                .SelectMany(constraintT => GetAllNamedTypeConstraints(constraintT));
                            return directConstraints.Concat(inheritedConstraints);
                        }

                        var constraints = GetAllNamedTypeConstraints(typeParameterArg);
                        foreach (INamedTypeSymbol constraint in constraints)
                        {
                            ProcessNamedTypeParamConstraint(constraint);
                        }
                    }
                }
            }
        }

        private static bool IsTypeInCurrentAssembly(string typeName, Compilation compilation, out INamedTypeSymbol? namedType)
        {
            namedType = null;
            var nameParts = typeName.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            return nameParts.Length >= 2 &&
                nameParts[1].Trim().Equals(compilation.AssemblyName, StringComparison.Ordinal) &&
                compilation.TryGetOrCreateTypeByMetadataName(nameParts[0].Trim(), out namedType) &&
                namedType.ContainingAssembly.Equals(compilation.Assembly);
        }
    }
}
