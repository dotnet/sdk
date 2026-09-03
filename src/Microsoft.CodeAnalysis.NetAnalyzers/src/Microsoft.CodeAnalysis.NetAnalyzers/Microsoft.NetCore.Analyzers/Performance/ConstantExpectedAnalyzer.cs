// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;
    public abstract partial class ConstantExpectedAnalyzer : DiagnosticAnalyzer
    {
        protected const string ConstantExpectedAttribute = nameof(ConstantExpectedAttribute);
        protected const string ConstantExpected = nameof(ConstantExpected);
        protected const string ConstantExpectedMin = "Min";
        protected const string ConstantExpectedMax = "Max";
        private static readonly LocalizableString s_localizableApplicationTitle = CreateLocalizableResourceString(nameof(ConstantExpectedApplicationTitle));
        private static readonly LocalizableString s_localizableApplicationDescription = CreateLocalizableResourceString(nameof(ConstantExpectedApplicationDescription));
        private static readonly LocalizableString s_localizableUsageTitle = CreateLocalizableResourceString(nameof(ConstantExpectedUsageTitle));
        private static readonly LocalizableString s_localizableUsageDescription = CreateLocalizableResourceString(nameof(ConstantExpectedUsageDescription));

        internal static class CA1856
        {
            internal const string Id = nameof(CA1856);
            internal const RuleLevel Level = RuleLevel.BuildError;
            internal static readonly DiagnosticDescriptor UnsupportedTypeRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableApplicationTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedNotSupportedMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableApplicationDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);

            internal static readonly DiagnosticDescriptor IncompatibleConstantTypeRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableApplicationTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedIncompatibleConstantTypeMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableApplicationDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);

            internal static readonly DiagnosticDescriptor InvalidBoundsRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableApplicationTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedInvalidBoundsMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableApplicationDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);

            internal static readonly DiagnosticDescriptor InvertedRangeRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableApplicationTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedInvertedRangeMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableApplicationDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);
        }

        internal static class CA1857
        {
            internal const string Id = nameof(CA1857);
            internal const RuleLevel Level = RuleLevel.BuildWarning;

            internal static readonly DiagnosticDescriptor ConstantOutOfBoundsRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableUsageTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedOutOfBoundsMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableUsageDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);

            internal static readonly DiagnosticDescriptor ConstantNotConstantRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableUsageTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedNotConstantMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableUsageDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);

            internal static readonly DiagnosticDescriptor ConstantInvalidConstantRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableUsageTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedInvalidMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableUsageDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);

            internal static readonly DiagnosticDescriptor AttributeExpectedRule = DiagnosticDescriptorHelper.Create(
                Id,
                s_localizableUsageTitle,
                CreateLocalizableResourceString(nameof(ConstantExpectedAttributExpectedMessage)),
                DiagnosticCategory.Performance,
                Level,
                description: s_localizableUsageDescription,
                isPortedFxCopRule: false,
                isDataflowRule: false);
        }
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            CA1856.UnsupportedTypeRule, CA1856.IncompatibleConstantTypeRule,
            CA1856.InvalidBoundsRule, CA1856.InvertedRangeRule,
            CA1857.ConstantOutOfBoundsRule, CA1857.ConstantInvalidConstantRule,
            CA1857.ConstantNotConstantRule, CA1857.AttributeExpectedRule);

        protected abstract DiagnosticHelper Helper { get; }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!ConstantExpectedContext.TryCreate(context.Compilation, out var constantExpectedContext))
            {
                return;
            }

            context.RegisterOperationAction(context => OnInvocation(context, constantExpectedContext), OperationKind.Invocation);
            context.RegisterSymbolAction(context => OnMethodSymbol(context, constantExpectedContext), SymbolKind.Method);
            RegisterAttributeSyntax(context, constantExpectedContext);
        }

        private static void OnMethodSymbol(SymbolAnalysisContext context, ConstantExpectedContext constantExpectedContext)
        {
            var methodSymbol = (IMethodSymbol)context.Symbol;
            if (methodSymbol.ExplicitInterfaceImplementations
                    .FirstOrDefault(methodSymbol.IsImplementationOfInterfaceMember) is { } explicitInterfaceMethod)
            {
                CheckParameters(methodSymbol.Parameters, explicitInterfaceMethod.Parameters);
            }
            else if (methodSymbol.OverriddenMethod is not null)
            {
                CheckParameters(methodSymbol.Parameters, methodSymbol.OverriddenMethod.Parameters);
            }
            else if (methodSymbol.IsImplementationOfAnyImplicitInterfaceMember(out IMethodSymbol interfaceMethodSymbol))
            {
                CheckParameters(methodSymbol.Parameters, interfaceMethodSymbol.Parameters);
            }

            void CheckParameters(ImmutableArray<IParameterSymbol> parameters, ImmutableArray<IParameterSymbol> baseParameters)
            {
                if (constantExpectedContext.ValidatesAttributeImplementedFromParent(parameters, baseParameters, out var diagnostics))
                {
                    return;
                }

                foreach (var diagnostic in diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static void OnInvocation(OperationAnalysisContext context, ConstantExpectedContext constantExpectedContext)
        {
            var invocation = (IInvocationOperation)context.Operation;

            foreach (var argument in invocation.Arguments)
            {
                if (!constantExpectedContext.TryCreateConstantExpectedParameter(argument.Parameter, out var argConstantParameter))
                {
                    continue;
                }

                var v = argument.Value.WalkDownConversion();
                if (v is IParameterReferenceOperation parameterReference &&
                    constantExpectedContext.TryCreateConstantExpectedParameter(parameterReference.Parameter, out var currConstantParameter))
                {
                    if (!argConstantParameter.ValidateParameterIsWithinRange(currConstantParameter, argument, out var parameterCheckDiagnostic))
                    {
                        context.ReportDiagnostic(parameterCheckDiagnostic);
                    }

                    continue;
                }

                if (v.ConstantValue is { } constantValue &&
                    !argConstantParameter.ValidateValue(argument, constantValue, out var valueDiagnostic))
                {
                    context.ReportDiagnostic(valueDiagnostic);
                }
            }
        }

        protected abstract void RegisterAttributeSyntax(CompilationStartAnalysisContext context, ConstantExpectedContext constantExpectedContext);

        protected void OnParameterWithConstantExpectedAttribute(IParameterSymbol parameter, ConstantExpectedContext constantExpectedContext, Action<Diagnostic> reportAction)
        {
            if (!constantExpectedContext.ValidateConstantExpectedParameter(parameter, Helper, out ImmutableArray<Diagnostic> diagnostics))
            {
                foreach (var diagnostic in diagnostics)
                {
                    reportAction(diagnostic);
                }
            }
        }

        protected sealed class ConstantExpectedContext
        {
            public INamedTypeSymbol AttributeSymbol { get; }

            public ConstantExpectedContext(INamedTypeSymbol attributeSymbol)
            {
                AttributeSymbol = attributeSymbol;
            }
            /// <summary>
            /// Validates for ConstantExpected attribute in base parameter and returns AttributeExpectedRule if the coresponding implementation parameter does not have it
            /// </summary>
            /// <param name="parameters"></param>
            /// <param name="baseParameters"></param>
            /// <param name="diagnostics">Non empty when method returns false</param>
            /// <returns></returns>
            public bool ValidatesAttributeImplementedFromParent(ImmutableArray<IParameterSymbol> parameters, ImmutableArray<IParameterSymbol> baseParameters, out ImmutableArray<Diagnostic> diagnostics)
            {
                var arraybuilder = ImmutableArray.CreateBuilder<Diagnostic>();
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    if (!IsConstantCompatible(parameter.Type))
                    {
                        continue;
                    }

                    var baseParameter = baseParameters[i];
                    if (HasConstantExpectedAttributeData(baseParameter) && !HasConstantExpectedAttributeData(parameter))
                    {
                        // mark the parameter including the type and name
                        var diagnostic = parameter.DeclaringSyntaxReferences[0].GetSyntax().CreateDiagnostic(CA1857.AttributeExpectedRule);
                        arraybuilder.Add(diagnostic);
                    }
                }

                diagnostics = arraybuilder.ToImmutable();
                return diagnostics.Length is 0;
            }

            private static bool IsConstantCompatible(ITypeSymbol type)
            {
                return type.SpecialType switch
                {
                    SpecialType.System_Char => true,
                    SpecialType.System_Byte => true,
                    SpecialType.System_UInt16 => true,
                    SpecialType.System_UInt32 => true,
                    SpecialType.System_UInt64 => true,
                    SpecialType.System_SByte => true,
                    SpecialType.System_Int16 => true,
                    SpecialType.System_Int32 => true,
                    SpecialType.System_Int64 => true,
                    SpecialType.System_Single => true,
                    SpecialType.System_Double => true,
                    SpecialType.System_Boolean => true,
                    SpecialType.System_String => true,
                    SpecialType.None when type.TypeKind == TypeKind.TypeParameter => true,
                    _ => false,
                };
            }

            /// <summary>
            /// Tries to create a ConstantExpectedParameter to represent the ConstantExpected attribute application
            /// </summary>
            /// <param name="parameterSymbol"></param>
            /// <param name="parameter"></param>
            /// <returns></returns>
            public bool TryCreateConstantExpectedParameter([NotNullWhen(true)] IParameterSymbol? parameterSymbol, [NotNullWhen(true)] out ConstantExpectedParameter? parameter)
            {
                var underlyingType = GetUnderlyingType(parameterSymbol);

                if (underlyingType == null ||
                    !TryGetConstantExpectedAttributeData(parameterSymbol, out var attributeData))
                {
                    parameter = null;
                    return false;
                }

                switch (underlyingType.SpecialType)
                {
                    case SpecialType.System_Char:
                        return UnmanagedHelper<char>.TryCreate(parameterSymbol, attributeData, char.MinValue, char.MaxValue, out parameter);
                    case SpecialType.System_Byte:
                        return UnmanagedHelper<ulong>.TryCreate(parameterSymbol, attributeData, byte.MinValue, byte.MaxValue, out parameter);
                    case SpecialType.System_UInt16:
                        return UnmanagedHelper<ulong>.TryCreate(parameterSymbol, attributeData, ushort.MinValue, ushort.MaxValue, out parameter);
                    case SpecialType.System_UInt32:
                        return UnmanagedHelper<ulong>.TryCreate(parameterSymbol, attributeData, uint.MinValue, uint.MaxValue, out parameter);
                    case SpecialType.System_UInt64:
                        return UnmanagedHelper<ulong>.TryCreate(parameterSymbol, attributeData, ulong.MinValue, ulong.MaxValue, out parameter);
                    case SpecialType.System_SByte:
                        return UnmanagedHelper<long>.TryCreate(parameterSymbol, attributeData, sbyte.MinValue, sbyte.MaxValue, out parameter);
                    case SpecialType.System_Int16:
                        return UnmanagedHelper<long>.TryCreate(parameterSymbol, attributeData, short.MinValue, short.MaxValue, out parameter);
                    case SpecialType.System_Int32:
                        return UnmanagedHelper<long>.TryCreate(parameterSymbol, attributeData, int.MinValue, int.MaxValue, out parameter);
                    case SpecialType.System_Int64:
                        return UnmanagedHelper<long>.TryCreate(parameterSymbol, attributeData, long.MinValue, long.MaxValue, out parameter);
                    case SpecialType.System_Single:
                        return UnmanagedHelper<float>.TryCreate(parameterSymbol, attributeData, float.MinValue, float.MaxValue, out parameter);
                    case SpecialType.System_Double:
                        return UnmanagedHelper<double>.TryCreate(parameterSymbol, attributeData, double.MinValue, double.MaxValue, out parameter);
                    case SpecialType.System_Boolean:
                        return UnmanagedHelper<bool>.TryCreate(parameterSymbol, attributeData, false, true, out parameter);
                    case SpecialType.System_String:
                        return StringConstantExpectedParameter.TryCreate(parameterSymbol, attributeData, out parameter);
                    default:
                        parameter = null;
                        return false;
                }
            }

            /// <summary>
            /// Validates that the parameter has a valid application of the ConstantExpected attributes. Returns diagnostics otherwise
            /// </summary>
            /// <param name="parameterSymbol"></param>
            /// <param name="helper"></param>
            /// <param name="diagnostics">not empty when method returns false</param>
            /// <returns></returns>
            public bool ValidateConstantExpectedParameter(IParameterSymbol parameterSymbol, DiagnosticHelper helper, out ImmutableArray<Diagnostic> diagnostics)
            {
                var underlyingType = GetUnderlyingType(parameterSymbol);

                if (underlyingType == null ||
                    !TryGetConstantExpectedAttributeData(parameterSymbol, out var attributeData))
                {
                    diagnostics = ImmutableArray<Diagnostic>.Empty;
                    return false;
                }

                switch (underlyingType.SpecialType)
                {
                    case SpecialType.System_Char:
                        return UnmanagedHelper<char>.Validate(parameterSymbol, attributeData, char.MinValue, char.MaxValue, helper, out diagnostics);
                    case SpecialType.System_Byte:
                        return UnmanagedHelper<ulong>.Validate(parameterSymbol, attributeData, byte.MinValue, byte.MaxValue, helper, out diagnostics);
                    case SpecialType.System_UInt16:
                        return UnmanagedHelper<ulong>.Validate(parameterSymbol, attributeData, ushort.MinValue, ushort.MaxValue, helper, out diagnostics);
                    case SpecialType.System_UInt32:
                        return UnmanagedHelper<ulong>.Validate(parameterSymbol, attributeData, uint.MinValue, uint.MaxValue, helper, out diagnostics);
                    case SpecialType.System_UInt64:
                        return UnmanagedHelper<ulong>.Validate(parameterSymbol, attributeData, ulong.MinValue, ulong.MaxValue, helper, out diagnostics);
                    case SpecialType.System_SByte:
                        return UnmanagedHelper<long>.Validate(parameterSymbol, attributeData, sbyte.MinValue, sbyte.MaxValue, helper, out diagnostics);
                    case SpecialType.System_Int16:
                        return UnmanagedHelper<long>.Validate(parameterSymbol, attributeData, short.MinValue, short.MaxValue, helper, out diagnostics);
                    case SpecialType.System_Int32:
                        return UnmanagedHelper<long>.Validate(parameterSymbol, attributeData, int.MinValue, int.MaxValue, helper, out diagnostics);
                    case SpecialType.System_Int64:
                        return UnmanagedHelper<long>.Validate(parameterSymbol, attributeData, long.MinValue, long.MaxValue, helper, out diagnostics);
                    case SpecialType.System_Single:
                        return UnmanagedHelper<float>.Validate(parameterSymbol, attributeData, float.MinValue, float.MaxValue, helper, out diagnostics);
                    case SpecialType.System_Double:
                        return UnmanagedHelper<double>.Validate(parameterSymbol, attributeData, double.MinValue, double.MaxValue, helper, out diagnostics);
                    case SpecialType.System_Boolean:
                        return UnmanagedHelper<bool>.Validate(parameterSymbol, attributeData, false, true, helper, out diagnostics);
                    case SpecialType.System_String:
                        return ValidateMinMaxIsNull(parameterSymbol, attributeData, helper, out diagnostics);
                    case SpecialType.None when parameterSymbol.Type.TypeKind == TypeKind.TypeParameter:
                        return ValidateMinMaxIsNull(parameterSymbol, attributeData, helper, out diagnostics);
                    default:
                        var syntax = attributeData.ApplicationSyntaxReference?.GetSyntax() ?? parameterSymbol.DeclaringSyntaxReferences[0].GetSyntax();
                        diagnostics = DiagnosticHelper.ParameterIsInvalid(parameterSymbol.Type.ToDisplayString(), syntax);
                        return false;
                }

                static bool ValidateMinMaxIsNull(IParameterSymbol parameterSymbol, AttributeData attributeData, DiagnosticHelper helper, out ImmutableArray<Diagnostic> diagnostics)
                {
                    ErrorKind errorFlags = 0;

                    foreach (var namedArg in attributeData.NamedArguments)
                    {
                        if (namedArg.Key.Equals(ConstantExpectedMin, StringComparison.Ordinal)
                            && !namedArg.Value.IsNull)
                        {
                            errorFlags |= ErrorKind.MinIsIncompatible;
                        }
                        else if (namedArg.Key.Equals(ConstantExpectedMax, StringComparison.Ordinal)
                            && !namedArg.Value.IsNull)
                        {
                            errorFlags |= ErrorKind.MaxIsIncompatible;
                        }
                    }

                    if (errorFlags is not 0)
                    {
                        var syntax = attributeData.ApplicationSyntaxReference?.GetSyntax() ?? parameterSymbol.DeclaringSyntaxReferences[0].GetSyntax();
                        diagnostics = helper.GetError(errorFlags, parameterSymbol, syntax, "null", "null");
                        return false;
                    }

                    diagnostics = ImmutableArray<Diagnostic>.Empty;
                    return true;
                }
            }

            private static ITypeSymbol? GetUnderlyingType(IParameterSymbol? parameterSymbol)
            {
                if (parameterSymbol?.Type.TypeKind is TypeKind.Enum)
                {
                    var enumType = (INamedTypeSymbol)parameterSymbol.Type;
                    return enumType.EnumUnderlyingType;
                }
                else
                {
                    return parameterSymbol?.Type;
                }
            }

            public bool TryGetConstantExpectedAttributeData([NotNullWhen(true)] IParameterSymbol? parameter, [NotNullWhen(true)] out AttributeData? attributeData)
            {
                attributeData = parameter?.GetAttribute(AttributeSymbol);
                return attributeData is not null;
            }

            private bool HasConstantExpectedAttributeData(IParameterSymbol parameter)
            {
                return parameter.HasAnyAttribute(AttributeSymbol);
            }

            public static bool TryCreate(Compilation compilation, [NotNullWhen(true)] out ConstantExpectedContext? constantExpectedContext)
            {
                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsCodeAnalysisConstantExpectedAttribute, out var attributeSymbol))
                {
                    constantExpectedContext = null;
                    return false;
                }

                constantExpectedContext = new ConstantExpectedContext(attributeSymbol);
                return true;
            }
        }

        /// <summary>
        /// Encodes a parameter with its ConstantExpected attribute rules
        /// </summary>
        protected abstract class ConstantExpectedParameter
        {
            protected ConstantExpectedParameter(IParameterSymbol parameter)
            {
                Parameter = parameter;
            }
            /// <summary>
            /// Parameter with the ConstantExpected attribute
            /// </summary>
            public IParameterSymbol Parameter { get; }

            /// <summary>
            /// Validates the provided constant value is within the constraints of ConstantExpected attribute set
            /// </summary>
            /// <param name="argument"></param>
            /// <param name="constant"></param>
            /// <param name="validationDiagnostics">Non empty when method returns false</param>
            /// <returns></returns>
            public abstract bool ValidateValue(IArgumentOperation argument, Optional<object?> constant, [NotNullWhen(false)] out Diagnostic? validationDiagnostics);

            public static bool ValidateConstant(IArgumentOperation argument, Optional<object?> constant, [NotNullWhen(false)] out Diagnostic? validationDiagnostics)
            {
                if (!constant.HasValue)
                {
                    validationDiagnostics = argument.CreateDiagnostic(CA1857.ConstantNotConstantRule);
                    return false;
                }

                validationDiagnostics = null;
                return true;
            }

            public abstract bool ValidateParameterIsWithinRange(ConstantExpectedParameter subsetCandidate, IArgumentOperation argument, [NotNullWhen(false)] out Diagnostic? validationDiagnostics);
            protected Diagnostic CreateConstantInvalidConstantRuleDiagnostic(IArgumentOperation argument) => argument.CreateDiagnostic(CA1857.ConstantInvalidConstantRule, Parameter.Type.ToDisplayString());
            protected static Diagnostic CreateConstantOutOfBoundsRuleDiagnostic(IArgumentOperation argument, string minText, string maxText) => argument.CreateDiagnostic(CA1857.ConstantOutOfBoundsRule, minText, maxText);
        }

        private sealed class StringConstantExpectedParameter : ConstantExpectedParameter
        {
            public StringConstantExpectedParameter(IParameterSymbol parameter) : base(parameter) { }

            public override bool ValidateParameterIsWithinRange(ConstantExpectedParameter subsetCandidate, IArgumentOperation argument, [NotNullWhen(false)] out Diagnostic? validationDiagnostics)
            {
                if (subsetCandidate is not StringConstantExpectedParameter)
                {
                    validationDiagnostics = CreateConstantInvalidConstantRuleDiagnostic(argument);
                    return false;
                }

                validationDiagnostics = null;
                return true;
            }

            public override bool ValidateValue(IArgumentOperation argument, Optional<object?> constant, [NotNullWhen(false)] out Diagnostic? validationDiagnostics)
            {
                if (!ValidateConstant(argument, constant, out validationDiagnostics))
                {
                    return false;
                }

                if (constant.Value is not string and not null)
                {
                    validationDiagnostics = CreateConstantInvalidConstantRuleDiagnostic(argument);
                    return false;
                }

                validationDiagnostics = null;
                return true;
            }

            public static bool TryCreate(IParameterSymbol parameterSymbol, AttributeData attributeData, [NotNullWhen(true)] out ConstantExpectedParameter? parameter)
            {
                var ac = AttributeConstant.Get(attributeData);
                if (ac.Min is not null || ac.Max is not null)
                {
                    parameter = null;
                    return false;
                }

                parameter = new StringConstantExpectedParameter(parameterSymbol);
                return true;
            }
        }

#pragma warning disable CA1815 // Override equals and operator equals on value types
        private readonly struct AttributeConstant
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            public object? Min { get; }
            public object? Max { get; }

            public AttributeConstant(object? min, object? max)
            {
                Min = min;
                Max = max;
            }

            public static AttributeConstant Get(AttributeData attributeData)
            {
                object? minConstant = null;
                object? maxConstant = null;

                foreach (var namedArg in attributeData.NamedArguments)
                {
                    if (namedArg.Key.Equals(ConstantExpectedMin, StringComparison.Ordinal))
                    {
                        minConstant = ToObject(namedArg.Value);
                    }
                    else if (namedArg.Key.Equals(ConstantExpectedMax, StringComparison.Ordinal))
                    {
                        maxConstant = ToObject(namedArg.Value);
                    }
                }

                return new AttributeConstant(minConstant, maxConstant);

                static object? ToObject(TypedConstant typedConstant)
                {
                    if (typedConstant.IsNull)
                    {
                        return null;
                    }

                    return typedConstant.Kind == TypedConstantKind.Array ? typedConstant.Values : typedConstant.Value;
                }
            }
        }

        protected abstract class DiagnosticHelper
        {
            public abstract Location? GetMinLocation(SyntaxNode attributeSyntax);
            public abstract Location? GetMaxLocation(SyntaxNode attributeSyntax);

            public static ImmutableArray<Diagnostic> ParameterIsInvalid(string expectedTypeName, SyntaxNode attributeSyntax) => ImmutableArray.Create(Diagnostic.Create(CA1856.UnsupportedTypeRule, attributeSyntax.GetLocation(), expectedTypeName));

            public Diagnostic MinIsIncompatible(string expectedTypeName, SyntaxNode attributeSyntax) => Diagnostic.Create(CA1856.IncompatibleConstantTypeRule, GetMinLocation(attributeSyntax)!, ConstantExpectedMin, expectedTypeName);

            public Diagnostic MaxIsIncompatible(string expectedTypeName, SyntaxNode attributeSyntax) => Diagnostic.Create(CA1856.IncompatibleConstantTypeRule, GetMaxLocation(attributeSyntax)!, ConstantExpectedMax, expectedTypeName);

            public Diagnostic MinIsOutOfRange(SyntaxNode attributeSyntax, string typeMinValue, string typeMaxValue) => Diagnostic.Create(CA1856.InvalidBoundsRule, GetMinLocation(attributeSyntax)!, ConstantExpectedMin, typeMinValue, typeMaxValue);

            public Diagnostic MaxIsOutOfRange(SyntaxNode attributeSyntax, string typeMinValue, string typeMaxValue) => Diagnostic.Create(CA1856.InvalidBoundsRule, GetMaxLocation(attributeSyntax)!, ConstantExpectedMax, typeMinValue, typeMaxValue);

            public static Diagnostic MinMaxIsInverted(SyntaxNode attributeSyntax) => Diagnostic.Create(CA1856.InvertedRangeRule, attributeSyntax.GetLocation());

            public ImmutableArray<Diagnostic> GetError(ErrorKind errorFlags, IParameterSymbol parameterSymbol, SyntaxNode attributeSyntax, string typeMinValue, string typeMaxValue)
            {
                switch (errorFlags)
                {
                    case ErrorKind.MinIsIncompatible:
                        return ImmutableArray.Create(MinIsIncompatible(parameterSymbol.Type.ToDisplayString(), attributeSyntax));
                    case ErrorKind.MaxIsIncompatible:
                        return ImmutableArray.Create(MaxIsIncompatible(parameterSymbol.Type.ToDisplayString(), attributeSyntax));
                    case ErrorKind.MinIsIncompatible | ErrorKind.MaxIsIncompatible:
                        var expectedTypeName = parameterSymbol.Type.ToDisplayString();
                        return ImmutableArray.Create(MinIsIncompatible(expectedTypeName, attributeSyntax), MaxIsIncompatible(expectedTypeName, attributeSyntax));
                    case ErrorKind.MinIsOutOfRange:
                        return ImmutableArray.Create(MinIsOutOfRange(attributeSyntax, typeMinValue, typeMaxValue));
                    case ErrorKind.MaxIsOutOfRange:
                        return ImmutableArray.Create(MaxIsOutOfRange(attributeSyntax, typeMinValue, typeMaxValue));
                    case ErrorKind.MinIsOutOfRange | ErrorKind.MaxIsOutOfRange:
                        return ImmutableArray.Create(MinIsOutOfRange(attributeSyntax, typeMinValue, typeMaxValue), MaxIsOutOfRange(attributeSyntax, typeMinValue, typeMaxValue));
                    case ErrorKind.MinIsOutOfRange | ErrorKind.MaxIsIncompatible:
                        return ImmutableArray.Create(MinIsOutOfRange(attributeSyntax, typeMinValue, typeMaxValue), MaxIsIncompatible(parameterSymbol.Type.ToDisplayString(), attributeSyntax));
                    case ErrorKind.MinIsIncompatible | ErrorKind.MaxIsOutOfRange:
                        return ImmutableArray.Create(MinIsIncompatible(parameterSymbol.Type.ToDisplayString(), attributeSyntax), MaxIsOutOfRange(attributeSyntax, typeMinValue, typeMaxValue));
                    case ErrorKind.MinMaxInverted:
                        return ImmutableArray.Create(MinMaxIsInverted(attributeSyntax));
                    default:
                        throw new ArgumentOutOfRangeException(nameof(errorFlags));
                }
            }
        }

        [Flags]
        protected enum ErrorKind
        {
            None = 0,
            /// <summary>
            /// mutually exclusive with MinIsIncompatible and MinMaxInverted
            /// </summary>
            MinIsOutOfRange = 1,
            /// <summary>
            /// mutually exclusive with MinIsOutOfRange and MinMaxInverted
            /// </summary>
            MinIsIncompatible = 1 << 2,
            /// <summary>
            /// mutually exclusive with MaxIsIncompatible and MinMaxInverted
            /// </summary>
            MaxIsOutOfRange = 1 << 3,
            /// <summary>
            /// mutually exclusive with MaxIsOutOfRange and MinMaxInverted
            /// </summary>
            MaxIsIncompatible = 1 << 4,
            /// <summary>
            /// mutually exclusive
            /// </summary>
            MinMaxInverted = 1 << 5,
        }
    }
}
