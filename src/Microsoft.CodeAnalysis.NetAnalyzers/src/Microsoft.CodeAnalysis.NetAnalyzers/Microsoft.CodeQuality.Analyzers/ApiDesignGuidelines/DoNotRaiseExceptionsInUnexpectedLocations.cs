// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1065: <inheritdoc cref="DoNotRaiseExceptionsInUnexpectedLocationsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotRaiseExceptionsInUnexpectedLocationsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1065";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInUnexpectedLocationsTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInUnexpectedLocationsDescription));

        internal static readonly DiagnosticDescriptor PropertyGetterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInUnexpectedLocationsMessagePropertyGetter)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled, // Could consider Suggestion level if we could exclude test code by default.
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor HasAllowedExceptionsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInUnexpectedLocationsMessageHasAllowedExceptions)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled, // Could consider Suggestion level if we could exclude test code by default.
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor NoAllowedExceptionsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInUnexpectedLocationsMessageNoAllowedExceptions)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled, // Could consider Suggestion level if we could exclude test code by default.
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(PropertyGetterRule, HasAllowedExceptionsRule, NoAllowedExceptionsRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                Compilation compilation = compilationStartContext.Compilation;
                INamedTypeSymbol? exceptionType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemException);
                if (exceptionType == null)
                {
                    return;
                }

                // Get a list of interesting categories of methods to analyze.
                List<MethodCategory> methodCategories = GetMethodCategories(compilation);

                compilationStartContext.RegisterOperationBlockStartAction(operationBlockContext =>
                {
                    if (operationBlockContext.OwningSymbol is not IMethodSymbol methodSymbol)
                    {
                        return;
                    }

                    // Find out if this given method is one of the interesting categories of methods.
                    // For example, certain Equals methods or certain accessors etc.
                    MethodCategory? methodCategory = methodCategories.FirstOrDefault(l => l.IsMatch(methodSymbol, compilation));
                    if (methodCategory == null)
                    {
                        return;
                    }

                    // For the interesting methods, register an operation action to catch all
                    // Throw statements.
                    operationBlockContext.RegisterOperationAction(operationContext =>
                    {
                        var throwOperation = (IThrowOperation)operationContext.Operation;
                        if (throwOperation.TryGetContainingAnonymousFunctionOrLocalFunction() is not null)
                        {
                            return;
                        }

                        // Get ThrowOperation's ExceptionType
                        if (throwOperation.GetThrownExceptionType() is INamedTypeSymbol thrownExceptionType && thrownExceptionType.DerivesFrom(exceptionType))
                        {
                            // If no exceptions are allowed or if the thrown exceptions is not an allowed one..
                            if (methodCategory.AllowedExceptions.IsEmpty || !methodCategory.AllowedExceptions.Any(n => thrownExceptionType.IsAssignableTo(n, compilation)))
                            {
                                operationContext.ReportDiagnostic(
                                    operationContext.Operation.Syntax.CreateDiagnostic(methodCategory.Rule, methodSymbol.Name, thrownExceptionType.Name));
                            }
                        }
                    }, OperationKind.Throw);
                });
            });
        }

        /// <summary>
        /// This object describes a class of methods where exception throwing statements should be analyzed.
        /// </summary>
        private class MethodCategory
        {
            /// <summary>
            /// Function used to determine whether a given method symbol falls into this category.
            /// </summary>
            private readonly Func<IMethodSymbol, Compilation, bool> _matchFunction;

            /// <summary>
            /// Determines if we should analyze non-public methods of a given type.
            /// </summary>
            private readonly bool _analyzeOnlyPublicMethods;

            /// <summary>
            /// The rule that should be fired if there is an exception in this kind of method.
            /// </summary>
            public DiagnosticDescriptor Rule { get; }

            /// <summary>
            /// List of exception types which are allowed to be thrown inside this category of method.
            /// This list will be empty if no exceptions are allowed.
            /// </summary>
            public ImmutableHashSet<ITypeSymbol> AllowedExceptions { get; }

            public MethodCategory(Func<IMethodSymbol, Compilation, bool> matchFunction, bool analyzeOnlyPublicMethods, DiagnosticDescriptor rule, params ITypeSymbol?[] allowedExceptionTypes)
            {
                _matchFunction = matchFunction;
                _analyzeOnlyPublicMethods = analyzeOnlyPublicMethods;
                this.Rule = rule;
                AllowedExceptions = allowedExceptionTypes.WhereNotNull().ToImmutableHashSet();
            }

            /// <summary>
            /// Checks if the given method belong this category
            /// </summary>
            public bool IsMatch(IMethodSymbol method, Compilation compilation)
            {
                // If we are supposed to analyze only public methods get the resultant visibility
                // i.e public method inside an internal class is not considered public.
                if (_analyzeOnlyPublicMethods && !method.IsExternallyVisible())
                {
                    return false;
                }

                return _matchFunction(method, compilation);
            }
        }

        private static List<MethodCategory> GetMethodCategories(Compilation compilation)
        {
            var methodCategories = new List<MethodCategory> {
                new MethodCategory(IsPropertyGetter, true,
                    PropertyGetterRule,
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemInvalidOperationException),
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotSupportedException)),

                new MethodCategory(IsIndexerGetter, true,
                    PropertyGetterRule,
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemInvalidOperationException),
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotSupportedException),
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentException),
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericKeyNotFoundException)),

                new MethodCategory(IsEventAccessor, true,
                    HasAllowedExceptionsRule,
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemInvalidOperationException),
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotSupportedException),
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentException)),

                new MethodCategory(IsGetHashCodeInterfaceImplementation, true,
                    HasAllowedExceptionsRule,
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentException)),

                new MethodCategory(IsEqualsOverrideOrInterfaceImplementation, true,
                    NoAllowedExceptionsRule),

                new MethodCategory(IsComparisonOperator, true,
                    NoAllowedExceptionsRule),

                new MethodCategory(IsGetHashCodeOverride, true,
                    NoAllowedExceptionsRule),

                new MethodCategory(IsToString, true,
                    NoAllowedExceptionsRule),

                new MethodCategory(IsImplicitCastOperator, true,
                    NoAllowedExceptionsRule),

                new MethodCategory(IsStaticConstructor, false,
                    NoAllowedExceptionsRule),

                new MethodCategory(IsFinalizer, false,
                    NoAllowedExceptionsRule),

                new MethodCategory(IMethodSymbolExtensions.IsDisposeImplementation, true,
                    NoAllowedExceptionsRule),
            };

            return methodCategories;
        }

        private static bool IsPropertyGetter(IMethodSymbol method, Compilation compilation)
        {
            return method.IsPropertyGetter();
        }

        private static bool IsIndexerGetter(IMethodSymbol method, Compilation compilation)
        {
            return method.IsIndexerGetter();
        }

        private static bool IsEventAccessor(IMethodSymbol method, Compilation compilation)
        {
            return method.IsEventAccessor();
        }

        private static bool IsEqualsOverrideOrInterfaceImplementation(IMethodSymbol method, Compilation compilation)
        {
            return method.IsObjectEqualsOverride() || IsEqualsInterfaceImplementation(method, compilation);
        }

        /// <summary>
        /// Checks if a given method implements IEqualityComparer.Equals or IEquatable.Equals.
        /// </summary>
        private static bool IsEqualsInterfaceImplementation(IMethodSymbol method, Compilation compilation)
        {
            if (method.Name != WellKnownMemberNames.ObjectEquals)
            {
                return false;
            }

            int paramCount = method.Parameters.Length;
            if (method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                (paramCount == 1 || paramCount == 2))
            {
                // Substitute the type of the first parameter of Equals in the generic interface and then check if that
                // interface method is implemented by the given method.
                INamedTypeSymbol? iEqualityComparer = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEqualityComparer1);
                if (method.IsImplementationOfInterfaceMethod(method.Parameters.First().Type, iEqualityComparer, WellKnownMemberNames.ObjectEquals))
                {
                    return true;
                }

                // Substitute the type of the first parameter of Equals in the generic interface and then check if that
                // interface method is implemented by the given method.
                INamedTypeSymbol? iEquatable = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1);
                if (method.IsImplementationOfInterfaceMethod(method.Parameters.First().Type, iEquatable, WellKnownMemberNames.ObjectEquals))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a given method implements IEqualityComparer.GetHashCode or IHashCodeProvider.GetHashCode.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="compilation"></param>
        /// <returns></returns>
        private static bool IsGetHashCodeInterfaceImplementation(IMethodSymbol method, Compilation compilation)
        {
            if (method.Name != WellKnownMemberNames.ObjectGetHashCode)
            {
                return false;
            }

            if (method.ReturnType.SpecialType == SpecialType.System_Int32 && method.Parameters.Length == 1)
            {
                // Substitute the type of the first parameter of Equals in the generic interface and then check if that
                // interface method is implemented by the given method.
                INamedTypeSymbol? iEqualityComparer = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEqualityComparer1);
                if (method.IsImplementationOfInterfaceMethod(method.Parameters.First().Type, iEqualityComparer, WellKnownMemberNames.ObjectGetHashCode))
                {
                    return true;
                }

                INamedTypeSymbol? iHashCodeProvider = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIHashCodeProvider);
                if (method.IsImplementationOfInterfaceMethod(null, iHashCodeProvider, WellKnownMemberNames.ObjectGetHashCode))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGetHashCodeOverride(IMethodSymbol method, Compilation compilation)
        {
            return method.IsGetHashCodeOverride();
        }

        private static bool IsToString(IMethodSymbol method, Compilation compilation)
        {
            return method.IsToStringOverride();
        }

        private static bool IsStaticConstructor(IMethodSymbol method, Compilation compilation)
        {
            return method.MethodKind == MethodKind.StaticConstructor;
        }

        private static bool IsFinalizer(IMethodSymbol method, Compilation compilation)
        {
            return method.IsFinalizer();
        }

        private static bool IsComparisonOperator(IMethodSymbol method, Compilation compilation)
        {
            if (!method.IsStatic || !method.IsPublic())
                return false;

            return method.Name switch
            {
                WellKnownMemberNames.EqualityOperatorName
                or WellKnownMemberNames.InequalityOperatorName
                or WellKnownMemberNames.LessThanOperatorName
                or WellKnownMemberNames.GreaterThanOperatorName
                or WellKnownMemberNames.LessThanOrEqualOperatorName
                or WellKnownMemberNames.GreaterThanOrEqualOperatorName => true,
                _ => false,
            };
        }

        private static bool IsImplicitCastOperator(IMethodSymbol method, Compilation compilation)
        {
            if (!method.IsStatic || !method.IsPublic())
                return false;
            return method.Name == WellKnownMemberNames.ImplicitConversionName;
        }
    }
}
