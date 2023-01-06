// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Lightup;
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
    // only suggest a replacement type if it reduces the number of virtual/interface calls

    /// <summary>
    /// Identifies locals/fields/parameters/return types which can be switched to a concrete type to eliminate virtual/interface dispatch.
    /// </summary>
    /// <remarks>
    /// First, we collect a bunch of state:
    ///
    ///   * For all locals/fields/parameters/returns within the named type, we create bags representing the types having been assigned to each.
    ///     This state will be used to know if we can 'upgrade' the element's type.
    ///
    ///   * For all locals/fields/parameters within the named type, we keep track of when they are used as 'this' for a virtual/interface call.
    ///     This state will be used to filter out diagnostics for those locals/fields/parameters which aren't inducing virtual/interface calls.
    ///     There's no sense in upgrading those elements if they aren't the source of virtual/interface calls.
    ///
    ///   * We keep track of all methods assigned to delegates so that we don't suggest changing the signature of these methods.
    ///
    /// Once all this state has been collected, we perform the actual analysis:
    ///
    ///   * Based on the bags of types being assigned to each local/field/parameter, if there is only one type being assigned and this
    ///   type is more specialized than what the element's type is, then we suggest upgrading the element's type accordingly.
    ///
    ///   * Based on the bags of types being returned by each method, if there is only one type being returned and this type is more specialized
    ///   than what was there before, then we suggest upgrading the return type accordingly.
    ///
    /// Several constraints are applied before we suggest modifying a method signature (either one of its parameters or its return type):
    ///
    ///   * The method cannot be implementing any interface.
    ///
    ///   * The method cannot be virtual, abstract, or be an override.
    ///
    ///   * The method must be private.
    ///
    ///   * The method must not have been assigned to a delegate.
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
                context.RegisterSymbolStartAction(context =>
                {
                    var coll = Collector.GetInstance(voidType);

                    // we accumulate a bunch of info in the collector object
                    context.RegisterOperationAction(context => coll.HandleInvocation((IInvocationOperation)context.Operation), OperationKind.Invocation);
                    context.RegisterOperationAction(context => coll.HandleSimpleAssignment((ISimpleAssignmentOperation)context.Operation), OperationKind.SimpleAssignment);
                    context.RegisterOperationAction(context => coll.HandleCoalesceAssignment((ICoalesceAssignmentOperation)context.Operation), OperationKind.CoalesceAssignment);
                    context.RegisterOperationAction(context => coll.HandleDeconstructionAssignment((IDeconstructionAssignmentOperation)context.Operation), OperationKind.DeconstructionAssignment);
                    context.RegisterOperationAction(context => coll.HandleFieldInitializer((IFieldInitializerOperation)context.Operation), OperationKind.FieldInitializer);
                    context.RegisterOperationAction(context => coll.HandleVariableDeclarator((IVariableDeclaratorOperation)context.Operation), OperationKind.VariableDeclarator);
                    context.RegisterOperationAction(context => coll.HandleDeclarationExpression((IDeclarationExpressionOperation)context.Operation), OperationKind.DeclarationExpression);
                    context.RegisterOperationAction(context => coll.HandleReturn((IReturnOperation)context.Operation), OperationKind.Return);

                    context.RegisterSymbolEndAction(context =>
                    {
                        // based on what we've collected, spit out relevant diagnostics
                        Report(context, coll);
                        Collector.ReturnInstance(coll, context.CancellationToken);
                    });
                }, SymbolKind.NamedType);
            });
        }

        /// <summary>
        /// Given all the accumulated analysis state, generate the diagnostics.
        /// </summary>
        private static void Report(SymbolAnalysisContext context, Collector coll)
        {
            // for all eligible private fields that are used as the receiver for a virtual call
            foreach (var field in coll.VirtualDispatchFields.Keys)
            {
                if (coll.FieldAssignments.TryGetValue(field, out var assignments))
                {
                    Report(field, field.Type, assignments, UseConcreteTypeForField);
                }
            }

            // for all eligible local variables that are used as the receiver for a virtual call
            foreach (var local in coll.VirtualDispatchLocals.Keys)
            {
                if (coll.LocalAssignments.TryGetValue(local, out var assignments))
                {
                    Report(local, local.Type, assignments, UseConcreteTypeForLocal);
                }
            }

            // for all eligible parameters that are used as the receiver for a virtual call
            foreach (var parameter in coll.VirtualDispatchParameters.Keys)
            {
                if (coll.ParameterAssignments.TryGetValue(parameter, out var assignments))
                {
                    if (parameter.ContainingSymbol is IMethodSymbol method)
                    {
                        if (CanUpgrade(method))
                        {
                            Report(parameter, parameter.Type, assignments, UseConcreteTypeForParameter);
                        }
                    }
                }
            }

            // for eligible all return types of private methods
            foreach (var pair in coll.MethodReturns)
            {
                var method = pair.Key;
                var returns = pair.Value;

                // only report the method if it never assigned to a delegate
                if (CanUpgrade(method))
                {
                    Report(method, method.ReturnType, returns, UseConcreteTypeForMethodReturn);
                }
            }

            void Report(ISymbol sym, ITypeSymbol fromType, PooledConcurrentSet<ITypeSymbol> assignments, DiagnosticDescriptor desc)
            {
                // a set of the values assigned to the given symbol
                using var types = PooledHashSet<ITypeSymbol>.GetInstance(assignments, SymbolEqualityComparer.Default);

                // 'void' is the magic value we use to represent null assignment
                var assignedNull = types.Remove(coll.Void!);

                // We currently only handle the case where there is only a single consistent type of value assigned to the
                // symbol. If there are multiple different types, we could try to find the common base for these, but it doesn't
                // seem worth the complication.
                if (types.Count == 1)
                {
                    var toType = types.Single();
                    if (assignedNull)
                    {
                        toType = toType.WithNullableAnnotation(Analyzer.Utilities.Lightup.NullableAnnotation.Annotated);
                    }

                    if (!toType.DerivesFrom(fromType.OriginalDefinition))
                    {
                        // can readily replace fromType by toType
                        return;
                    }

                    if (toType.TypeKind == TypeKind.Class
                        && !SymbolEqualityComparer.Default.Equals(fromType, toType)
                        && toType.SpecialType != SpecialType.System_Object
                        && toType.SpecialType != SpecialType.System_Delegate)
                    {
                        var fromTypeName = GetTypeName(fromType);
                        var toTypeName = GetTypeName(toType);
                        var diagnostic = sym.CreateDiagnostic(desc, sym.Name, fromTypeName, toTypeName);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }

            bool CanUpgrade(IMethodSymbol methodSym) => !coll.MethodsAssignedToDelegate.ContainsKey(methodSym);

            static string GetTypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        }
    }
}
