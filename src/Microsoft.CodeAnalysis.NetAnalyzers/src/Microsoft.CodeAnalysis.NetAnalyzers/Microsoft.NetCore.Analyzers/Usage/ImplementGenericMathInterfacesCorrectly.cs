// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Usage
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2260: <inheritdoc cref="ImplementGenericMathInterfacesCorrectlyTitle"/>
    /// Some generic math interfaces require the derived type itself to be used for the self recurring type parameter, enforces that requirement
    /// </summary>
    public abstract class ImplementGenericMathInterfacesCorrectly : DiagnosticAnalyzer
    {
        private const string RuleId = "CA2260";

        internal static readonly DiagnosticDescriptor GMIRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ImplementGenericMathInterfacesCorrectlyTitle)),
            CreateLocalizableResourceString(nameof(ImplementGenericMathInterfacesCorrectlyMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            description: CreateLocalizableResourceString(nameof(ImplementGenericMathInterfacesCorrectlyDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        private static readonly ImmutableHashSet<string> s_knownInterfaces = ImmutableHashSet.Create("IParsable`1", "ISpanParsable`1", "IAdditionOperators`3", "IAdditiveIdentity`2",
            "IBinaryFloatingPointIeee754`1", "IBinaryInteger`1", "IBinaryNumber`1", "IBitwiseOperators`3", "IComparisonOperators`3", "IDecrementOperators`1", "IDivisionOperators`3",
            "IEqualityOperators`3", "IExponentialFunctions`1", "IFloatingPointIeee754`1", "IFloatingPoint`1", "IHyperbolicFunctions`1", "IIncrementOperators`1", "ILogarithmicFunctions`1",
            "IMinMaxValue`1", "IModulusOperators`3", "IMultiplicativeIdentity`2", "IMultiplyOperators`3", "INumberBase`1", "INumber`1", "IPowerFunctions`1", "IRootFunctions`1", "IShiftOperators`3",
            "ISignedNumber`1", "ISubtractionOperators`3", "ITrigonometricFunctions`1", "IUnaryNegationOperators`2", "IUnaryPlusOperators`2", "IUnsignedNumber`1");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GMIRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIParsable1, out var iParsableInterface) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNumericsINumber1, out var iNumberInterface))
                {
                    return;
                }

                context.RegisterSymbolAction(context =>
                {
                    if (context.Symbol is INamedTypeSymbol ntSymbol)
                    {
                        AnalyzeSymbol(context, ntSymbol, iParsableInterface.ContainingNamespace, iNumberInterface.ContainingNamespace);
                    }
                }, SymbolKind.NamedType);
            });
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol symbol, INamespaceSymbol systemNS, INamespaceSymbol systemNumericsNS)
        {
            foreach (INamedTypeSymbol anInterface in symbol.Interfaces)
            {
                if (IsKnownInterfaceInTheChain(anInterface) &&
                    FirstTypeParameterNameIsNotTheSymbolName(symbol, anInterface) &&
                    (!symbol.IsGenericType || NotConstrainedToTheInterfaceOrSelf(anInterface, symbol)))
                {
                    SyntaxNode? typeParameter = FindTheTypeArgumentOfTheInterfaceFromTypeDeclaration(symbol, anInterface);
                    context.ReportDiagnostic(CreateDiagnostic(GMIRule, typeParameter, anInterface.OriginalDefinition.ToDisplayString(
                        SymbolDisplayFormat.MinimallyQualifiedFormat), anInterface.OriginalDefinition.TypeParameters[0].Name, symbol));
                }
            }

            INamedTypeSymbol? baseType = symbol.BaseType;
            while (baseType != null && baseType.IsGenericType)
            {
                foreach (INamedTypeSymbol anInterface in baseType.Interfaces)
                {
                    if (IsKnownInterfaceInTheChain(anInterface) &&
                        FirstTypeParameterNameIsNotTheSymbolName(symbol, anInterface) &&
                        (!symbol.IsGenericType || NotConstrainedToTheInterfaceOrSelf(anInterface, symbol)))
                    {
                        SyntaxNode? typeParameter = FindTheTypeArgumentOfTheInterfaceFromTypeDeclaration(symbol, symbol.BaseType!);
                        context.ReportDiagnostic(CreateDiagnostic(GMIRule, typeParameter, symbol.BaseType!.OriginalDefinition.ToDisplayString(
                            SymbolDisplayFormat.MinimallyQualifiedFormat), symbol.BaseType.OriginalDefinition.TypeParameters[0].Name, symbol));
                    }
                }

                baseType = baseType.BaseType;
            }

            bool IsKnownInterfaceInTheChain(INamedTypeSymbol anInterface)
            {
                if (anInterface.IsGenericType)
                {
                    if (IsKnownInterface(anInterface, systemNS, systemNumericsNS))
                    {
                        return true;
                    }
                    else
                    {
                        foreach (INamedTypeSymbol parentInterface in anInterface.Interfaces)
                        {
                            if (IsKnownInterfaceInTheChain(parentInterface))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            static bool NotConstrainedToTheInterfaceOrSelf(INamedTypeSymbol anInterface, INamedTypeSymbol symbol)
            {
                foreach (var typeParameter in symbol.TypeParameters)
                {
                    foreach (var constraint in typeParameter.ConstraintTypes)
                    {
                        if (constraint.Equals(symbol) ||
                            constraint.Equals(anInterface, SymbolEqualityComparer.Default))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        private static Diagnostic CreateDiagnostic(DiagnosticDescriptor GMIRule, SyntaxNode? parameter,
            string genericDeclaration, string parameterName, INamedTypeSymbol symbol) =>
                parameter is null ? symbol.CreateDiagnostic(GMIRule, genericDeclaration, parameterName, symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)) :
                    parameter.CreateDiagnostic(GMIRule, genericDeclaration, parameterName, symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

        protected abstract SyntaxNode? FindTheTypeArgumentOfTheInterfaceFromTypeDeclaration(ISymbol typeSymbol, ISymbol theInterfaceSymbol);

        private static bool IsKnownInterface(INamedTypeSymbol anInterface, INamespaceSymbol systemNS, INamespaceSymbol systemNumericsNS)
        {
            var iNamespace = anInterface.ContainingNamespace;

            return s_knownInterfaces.Contains(anInterface.MetadataName) &&
                   (iNamespace.Equals(systemNS, SymbolEqualityComparer.Default) ||
                    iNamespace.Equals(systemNumericsNS, SymbolEqualityComparer.Default));
        }

        private static bool FirstTypeParameterNameIsNotTheSymbolName(INamedTypeSymbol symbol, INamedTypeSymbol anInterface) =>
            anInterface.TypeArguments[0].Name != symbol.Name;
    }
}
