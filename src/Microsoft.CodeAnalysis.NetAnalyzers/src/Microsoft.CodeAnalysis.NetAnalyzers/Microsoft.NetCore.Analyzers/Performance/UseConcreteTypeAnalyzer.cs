// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    // Ideas for the future
    // ====================
    // Detect arrays/collections of interface types which could be replaced with arrays/collections of concrete types
    // Suggest to upgrade members of tuples returned from a method
    // Suggest to upgrade Task<IFoo> to Task<Foo>
    // Only suggest a replacement type if it reduces the number of virtual/interface calls

    /// <summary>
    /// Identifies locals/fields/properties/parameters/return types which can be switched to a concrete type to eliminate virtual/interface dispatch.
    /// </summary>
    /// <remarks>
    /// First, we collect a bunch of state:
    ///
    ///   * For all locals/fields/properties/parameters/returns within the named type, we create bags representing the types having been assigned to each.
    ///     This state will be used to know if we can 'upgrade' the element's type.
    ///
    ///   * For all locals/fields/properties/parameters within the named type, we keep track of when they are used as 'this' for a virtual/interface call.
    ///     This state will be used to filter out diagnostics for those locals/fields/parameters which aren't inducing virtual/interface calls.
    ///     There's no sense in upgrading those elements if they aren't the source of virtual/interface calls.
    ///
    ///   * We keep track of all methods assigned to delegates so that we don't suggest changing the signature of these methods.
    ///
    /// Once all this state has been collected, we perform the actual analysis:
    ///
    ///   * Based on the bags of types being assigned to each local/field/property/parameter, if there is only one type being assigned and this
    ///   type is more specialized than what the element's type is, then we suggest upgrading the element's type accordingly.
    ///
    ///   * Based on the bags of types being returned by each method, if there is only one type being returned and this type is more specialized
    ///   than what was there before, then we suggest upgrading the return type accordingly.
    ///
    /// Several constraints are applied before we suggest modifying a method or property signature (either one of its parameters or its return type):
    ///
    ///   * The method/property cannot be implementing any interface.
    ///
    ///   * The method/property cannot be virtual, abstract, or be an override.
    ///
    ///   * The method must not have been assigned to a delegate.
    ///   
    ///   * The method must not be the implementation of a partial method definition.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed partial class UseConcreteTypeAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1859";

        internal static readonly DiagnosticDescriptor UseConcreteTypeForMethodReturn = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseConcreteTypeTitle)),
            CreateLocalizableResourceString(nameof(UseConcreteTypeForMethodReturnMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseConcreteTypeDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UseConcreteTypeForProperty = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseConcreteTypeTitle)),
            CreateLocalizableResourceString(nameof(UseConcreteTypeForPropertyMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseConcreteTypeDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UseConcreteTypeForParameter = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseConcreteTypeTitle)),
            CreateLocalizableResourceString(nameof(UseConcreteTypeForParameterMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseConcreteTypeDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UseConcreteTypeForLocal = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseConcreteTypeTitle)),
            CreateLocalizableResourceString(nameof(UseConcreteTypeForLocalMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseConcreteTypeDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UseConcreteTypeForField = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseConcreteTypeTitle)),
            CreateLocalizableResourceString(nameof(UseConcreteTypeForFieldMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(UseConcreteTypeDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            UseConcreteTypeForField,
            UseConcreteTypeForLocal,
            UseConcreteTypeForMethodReturn,
            UseConcreteTypeForParameter);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(context =>
            {
                var voidType = context.Compilation.GetSpecialType(SpecialType.System_Void);
                var publicOrInternalColl = Collector.GetInstance(voidType, symbol => symbol.IsInSource() && context.Options.MatchesConfiguredVisibility(UseConcreteTypeForMethodReturn, symbol, context.Compilation, SymbolVisibilityGroup.Private));

                context.RegisterSymbolStartAction(context =>
                {
                    var namedType = (INamedTypeSymbol)context.Symbol;
                    if (namedType.TypeKind == TypeKind.Interface)
                    {
                        // nothing to do here
                        return;
                    }

                    var coll = Collector.GetInstance(voidType, symbol => symbol.IsInSource() && context.Options.MatchesConfiguredVisibility(UseConcreteTypeForMethodReturn, symbol, context.Compilation, SymbolVisibilityGroup.Private));

                    // we accumulate a bunch of info in the collector object
                    context.RegisterOperationAction(context => coll.HandleInvocation((IInvocationOperation)context.Operation), OperationKind.Invocation);
                    context.RegisterOperationAction(context => coll.HandleSimpleAssignment((ISimpleAssignmentOperation)context.Operation), OperationKind.SimpleAssignment);
                    context.RegisterOperationAction(context => coll.HandleCoalesceAssignment((ICoalesceAssignmentOperation)context.Operation), OperationKind.CoalesceAssignment);
                    context.RegisterOperationAction(context => coll.HandleDeconstructionAssignment((IDeconstructionAssignmentOperation)context.Operation), OperationKind.DeconstructionAssignment);
                    context.RegisterOperationAction(context => coll.HandleFieldInitializer((IFieldInitializerOperation)context.Operation), OperationKind.FieldInitializer);
                    context.RegisterOperationAction(context => coll.HandlePropertyInitializer((IPropertyInitializerOperation)context.Operation), OperationKind.PropertyInitializer);
                    context.RegisterOperationAction(context => coll.HandlePropertyReference((IPropertyReferenceOperation)context.Operation), OperationKind.PropertyReference);
                    context.RegisterOperationAction(context => coll.HandleVariableDeclarator((IVariableDeclaratorOperation)context.Operation), OperationKind.VariableDeclarator);
                    context.RegisterOperationAction(context => coll.HandleDeclarationExpression((IDeclarationExpressionOperation)context.Operation), OperationKind.DeclarationExpression);
                    context.RegisterOperationAction(context => coll.HandleReturn((IReturnOperation)context.Operation), OperationKind.Return);

                    context.RegisterSymbolEndAction(context =>
                    {
                        // remove any collected state having to do with non-private symbols, we'll tackle that later
                        publicOrInternalColl.ExtractNonPrivate(coll);

                        // based on what we've collected, spit out relevant diagnostics for private symbols
                        Report(context.ReportDiagnostic, coll, context.Compilation);
                        Collector.ReturnInstance(coll, context.CancellationToken);
                    });
                }, SymbolKind.NamedType);

                context.RegisterCompilationEndAction(context =>
                {
                    // based on what we've collected, spit out relevant diagnostics for public or internal symbols
                    Report(context.ReportDiagnostic, publicOrInternalColl, context.Compilation);
                    Collector.ReturnInstance(publicOrInternalColl, context.CancellationToken);
                });
            });
        }

        /// <summary>
        /// Given all the accumulated analysis state, generate the diagnostics.
        /// </summary>
        private static void Report(Action<Diagnostic> reportDiag, Collector coll, Compilation compilation)
        {
            // for all eligible fields that are used as the receiver for a virtual call
            foreach (var pair in coll.VirtualDispatchFields)
            {
                var field = pair.Key;
                var methods = pair.Value;

                if (coll.FieldAssignments.TryGetValue(field, out var assignments))
                {
                    Evaluate(field, field.Type, assignments, methods, UseConcreteTypeForField);
                }
            }

            // for all eligible properties that are used as the receiver for a virtual call
            foreach (var pair in coll.VirtualDispatchProperties)
            {
                var property = pair.Key;
                var methods = pair.Value;

                if (coll.PropertyAssignments.TryGetValue(property, out var assignments))
                {
                    Evaluate(property, property.Type, assignments, methods, UseConcreteTypeForProperty);
                }
            }

            // for all eligible local variables that are used as the receiver for a virtual call
            foreach (var pair in coll.VirtualDispatchLocals)
            {
                var local = pair.Key;
                var methods = pair.Value;

                if (coll.LocalAssignments.TryGetValue(local, out var assignments))
                {
                    Evaluate(local, local.Type, assignments, methods, UseConcreteTypeForLocal);
                }
            }

            // for all eligible parameters that are used as the receiver for a virtual call
            foreach (var pair in coll.VirtualDispatchParameters)
            {
                var parameter = pair.Key;
                var methods = pair.Value;

                if (coll.ParameterAssignments.TryGetValue(parameter, out var assignments))
                {
                    if (parameter.ContainingSymbol is IMethodSymbol method)
                    {
                        if (CanUpgrade(method))
                        {
                            Evaluate(parameter, parameter.Type, assignments, methods, UseConcreteTypeForParameter);
                        }
                    }
                }
            }

            // for all eligible return types of methods
            foreach (var pair in coll.MethodReturns)
            {
                var method = pair.Key;
                var returns = pair.Value;

                // only report the method if it is never assigned to a delegate
                if (CanUpgrade(method))
                {
                    Evaluate(method, method.ReturnType, returns, null, UseConcreteTypeForMethodReturn);
                }
            }

            void Evaluate(ISymbol affectedSymbol, ITypeSymbol fromType, PooledConcurrentSet<ITypeSymbol> typesAssigned, PooledConcurrentSet<IMethodSymbol>? targets, DiagnosticDescriptor desc)
            {
                // set of the values assigned to the given symbol
                using var types = PooledHashSet<ITypeSymbol>.GetInstance(typesAssigned, SymbolEqualityComparer.Default);

                // 'void' is the magic value we use to represent null assignment
                var assignedNull = types.Remove(coll.Void!);

                // We currently only handle the case where there is a single consistent type of value assigned to the
                // symbol. If there are multiple different types, we could try to find the common base for these, but it doesn't
                // seem worth the complication.
                if (types.Count != 1)
                {
                    return;
                }

                var toType = types.Single();
                if (assignedNull || fromType.NullableAnnotation == NullableAnnotation.Annotated)
                {
                    toType = toType.WithNullableAnnotation(NullableAnnotation.Annotated);
                }

                if (!toType.DerivesFrom(fromType.OriginalDefinition))
                {
                    // can't readily replace fromType by toType
                    return;
                }

                // if any of the methods that are invoked on toType are explicit implementations of interface methods, then we don't want
                // to recommend upgrading the type otherwise it would break those call sites
                if (targets != null)
                {
                    foreach (var t in targets)
                    {
                        var check = toType;
                        while (check != null)
                        {
                            foreach (var m in check.GetMembers())
                            {
                                if (m.IsImplementationOfAnyExplicitInterfaceMember())
                                {
                                    if (m.IsImplementationOfInterfaceMember(t))
                                    {
                                        return;
                                    }
                                }
                            }

                            check = check.BaseType;
                        }
                    }
                }

                // if the toType or any of its base types introduce methods with the same name as any of the target methods,
                // we shouldn't recommend to upgrade the type. This is because these new overloads can lead to binding to the
                // wrong methods in the case of an upgrade from a base type to a derived type.
                if (targets != null && fromType.TypeKind != TypeKind.Interface)
                {
                    using var targetNames = PooledHashSet<string>.GetInstance(targets.Select(t => t.Name));
                    var check = toType;
                    while (check != null && check != fromType)
                    {
                        foreach (var m in check.GetMembers())
                        {
                            if (m is IMethodSymbol ms)
                            {
                                if (!ms.IsDefinition || ms.IsOverride)
                                {
                                    // those are OK, they won't cause trouble
                                    continue;
                                }

                                if (targetNames.Contains(ms.Name))
                                {
                                    // OK, we found a match, so we're giving up on this potential upgrade
                                    return;
                                }
                            }
                        }

                        check = check.BaseType;
                    }
                }

                if (toType.TypeKind is not TypeKind.Class and not TypeKind.Array and not TypeKind.Struct)
                {
                    // we only deal with classes, arrays, or structs
                    return;
                }

                if (SymbolEqualityComparer.Default.Equals(fromType, toType))
                {
                    // don't recommend upgrading the type to itself
                    return;
                }

                if (toType.SpecialType is SpecialType.System_Object or SpecialType.System_Delegate)
                {
                    // skip these special types
                    return;
                }

                if (affectedSymbol.IsExternallyVisible() && !toType.IsExternallyVisible())
                {
                    // if the affected symbol is externally visible, then the suggested type must be externally visible too
                    return;
                }

                if (!HasEquivalentOrGreaterVisibilityToSymbol(compilation, toType, affectedSymbol))
                {
                    // the suggested type must have equal or greater visibility than the affected symbol.
                    return;
                }

                var fromTypeName = GetTypeName(fromType);
                var toTypeName = GetTypeName(toType);
                var diagnostic = affectedSymbol.CreateDiagnostic(desc, affectedSymbol.Name, fromTypeName, toTypeName);
                reportDiag(diagnostic);
            }

            // ensures that the type can be referenced from any code that can also reference the symbol
            static bool HasEquivalentOrGreaterVisibilityToSymbol(Compilation compilation, ITypeSymbol type, ISymbol affectedSymbol)
            {
                var container = affectedSymbol.ContainingType;
                while (container != null)
                {
                    if (!compilation.IsSymbolAccessibleWithin(affectedSymbol, container))
                    {
                        // the affected symbol is no longer visible, so we've passed the gauntlet
                        return true;
                    }

                    if (!compilation.IsSymbolAccessibleWithin(type, container))
                    {
                        // if the type can't be reached here, we can't proceed
                        return false;
                    }

                    container = container.ContainingType;
                }

                if (!compilation.IsSymbolAccessibleWithin(affectedSymbol, affectedSymbol.ContainingAssembly))
                {
                    // if the affected symbol is not visible at the assembly level, we're done
                    return true;
                }

                // final check
                return compilation.IsSymbolAccessibleWithin(type, affectedSymbol.ContainingAssembly);
            }

            bool CanUpgrade(IMethodSymbol methodSym) => !coll.MethodsAssignedToDelegate.ContainsKey(methodSym);

            static string GetTypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        }
    }
}
