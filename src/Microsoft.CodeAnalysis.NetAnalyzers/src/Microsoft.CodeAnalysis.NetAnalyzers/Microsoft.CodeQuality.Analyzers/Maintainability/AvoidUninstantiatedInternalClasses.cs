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
    /// <summary>
    /// CA1812: Avoid uninstantiated internal classes
    /// </summary>
    public abstract class AvoidUninstantiatedInternalClassesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1812";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUninstantiatedInternalClassesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUninstantiatedInternalClassesMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidUninstantiatedInternalClassesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.Disabled,    // Code coverage tools provide superior results when done correctly.
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isReportedAtCompilationEnd: true);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

                // If the assembly being built by this compilation exposes its internals to
                // any other assembly, don't report any "uninstantiated internal class" errors.
                // If we were to report an error for an internal type that is not instantiated
                // by this assembly, and then it turned out that the friend assembly did
                // instantiate the type, that would be a false positive. We've decided it's
                // better to have false negatives (which would happen if the type were *not*
                // instantiated by any friend assembly, but we didn't report the issue) than
                // to have false positives.
                var internalsVisibleToAttributeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesInternalsVisibleToAttribute);
                if (compilation.Assembly.HasAttribute(internalsVisibleToAttributeSymbol))
                {
                    return;
                }

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
                        !IsOkToBeUninstantiated(type, compilation,
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
                    var constructedClass = (INamedTypeSymbol)expr.Type;

                    if (!constructedClass.IsGenericType || constructedClass.IsUnboundGenericType)
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
                        context.ReportDiagnostic(type.CreateDiagnostic(Rule, type.FormatMemberName()));
                    }
                });

                return;

                // Local functions

                bool CanBeCoClassAttributeContext(INamedTypeSymbol type)
                     => coClassAttributeSymbol != null && type.TypeKind == TypeKind.Interface;

                INamedTypeSymbol? FindTypeIfCoClassAttribute(AttributeData attribute)
                {
                    RoslynDebug.Assert(coClassAttributeSymbol != null);

                    if (attribute.AttributeClass.Equals(coClassAttributeSymbol) &&
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

                    if (!attribute.AttributeClass.Equals(debuggerTypeProxyAttributeSymbol))
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
            Compilation compilation,
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

            // Ignore type generated for holding top level statements
            if (type.IsTopLevelStatementsEntryPointType())
            {
                return true;
            }

            // The type containing the assembly's entry point is OK.
            if (ContainsEntryPoint(type, compilation))
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
            return (mef1ExportAttributeSymbol != null && type.HasAttribute(mef1ExportAttributeSymbol))
                || (mef2ExportAttributeSymbol != null && type.HasAttribute(mef2ExportAttributeSymbol));
        }

        private static bool ContainsEntryPoint(INamedTypeSymbol type, Compilation compilation)
        {
            // If this type doesn't live in an application assembly (.exe), it can't contain
            // the entry point.
            if (compilation.Options.OutputKind is not OutputKind.ConsoleApplication and
                not OutputKind.WindowsApplication and
                not OutputKind.WindowsRuntimeApplication)
            {
                return false;
            }

            var wellKnowTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
            var taskSymbol = wellKnowTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
            var genericTaskSymbol = wellKnowTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask1);

            // TODO: Handle the case where Compilation.Options.MainTypeName matches this type.
            // TODO: Test: can't have type parameters.
            // TODO: Main in nested class? If allowed, what name does it have?
            // TODO: Test that parameter is array of int.
            return type.GetMembers("Main")
                .Where(m => m is IMethodSymbol)
                .Cast<IMethodSymbol>()
                .Any(m => IsEntryPoint(m, taskSymbol, genericTaskSymbol));
        }

        private static bool IsEntryPoint(IMethodSymbol method, ITypeSymbol? taskSymbol, ITypeSymbol? genericTaskSymbol)
        {
            if (!method.IsStatic)
            {
                return false;
            }

            if (!IsSupportedReturnType(method, taskSymbol, genericTaskSymbol))
            {
                return false;
            }

            if (!method.Parameters.Any())
            {
                return true;
            }

            if (method.Parameters.HasMoreThan(1))
            {
                return false;
            }

            return true;
        }

        private static bool IsSupportedReturnType(IMethodSymbol method, ITypeSymbol? taskSymbol, ITypeSymbol? genericTaskSymbol)
        {
            if (method.ReturnType.SpecialType == SpecialType.System_Int32)
            {
                return true;
            }

            if (method.ReturnsVoid)
            {
                return true;
            }

            if (taskSymbol != null && Equals(method.ReturnType, taskSymbol))
            {
                return true;
            }

            if (genericTaskSymbol != null && Equals(method.ReturnType.OriginalDefinition, genericTaskSymbol) && ((INamedTypeSymbol)method.ReturnType).TypeArguments.Single().SpecialType == SpecialType.System_Int32)
            {
                return true;
            }

            return false;
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
