// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2021: Do not call Enumerable.Cast or Enumerable.OfType with incompatible types.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2021";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesDescription));

        private static readonly LocalizableString s_localizableCastMessage = CreateLocalizableResourceString(nameof(DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesMessageCast));
        private static readonly LocalizableString s_localizableOfTypeMessage = CreateLocalizableResourceString(nameof(DoNotCallEnumerableCastOrOfTypeWithIncompatibleTypesMessageOfType));

        internal static DiagnosticDescriptor CastRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableCastMessage,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.BuildWarning,
                                                                             s_localizableDescription,
                                                                             isPortedFxCopRule: false,
                                                                             isDataflowRule: false);

        internal static DiagnosticDescriptor OfTypeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableOfTypeMessage,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.BuildWarning,
                                                                             s_localizableDescription,
                                                                             isPortedFxCopRule: false,
                                                                             isDataflowRule: false);

        private static readonly ImmutableArray<(string MethodName, DiagnosticDescriptor Rule)> s_methodMetadataNames = ImmutableArray.Create(
            (nameof(Enumerable.Cast), CastRule),
            (nameof(Enumerable.OfType), OfTypeRule)
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(OfTypeRule, CastRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable, out var enumerableType))
                {
                    return;
                }

#pragma warning disable IDE0004 // Remove Unnecessary Cast - Removal of cast leads to CS8714 compiler warning.
                var methodRuleDictionary = s_methodMetadataNames
                    .SelectMany(m => enumerableType
                        .GetMembers(m.MethodName)
                        .OfType<IMethodSymbol>()
                        .Where(method => method.IsExtensionMethod
                                      && method.TypeParameters.HasExactly(1)
                                      && method.Parameters.HasExactly(1)
                                      && method.Parameters[0].Type.OriginalDefinition.SpecialType
                                            == SpecialType.System_Collections_IEnumerable
                              )
                        .Select(method => (method, m.Rule)))
                    .ToImmutableDictionary(key => (ISymbol)key.method, v => v.Rule, SymbolEqualityComparer.Default);
#pragma warning restore IDE0004 // Remove Unnecessary Cast

                if (methodRuleDictionary.IsEmpty)
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var invocation = (IInvocationOperation)context.Operation;

                    var targetMethod = (invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod).OriginalDefinition;

                    if (!methodRuleDictionary.TryGetValue(targetMethod, out var rule))
                    {
                        return;
                    }

                    var instanceArg = invocation.GetInstance(); // "this" argument of an extension method

                    static ITypeSymbol? GetIEnumerableTParam(ITypeSymbol type)
                    {
                        if (type is not INamedTypeSymbol argIEnumerableType
                            || !argIEnumerableType.TypeArguments.HasExactly(1))
                        {
                            return null;
                        }

                        return argIEnumerableType.TypeArguments[0];
                    }

                    static ITypeSymbol? FindElementType(IOperation? operation)
                    {
                        if (operation?.Type is null)
                        {
                            return null;
                        }

                        if (operation.Kind == OperationKind.ArrayCreation)
                        {
                            return (operation.Type as IArrayTypeSymbol)?.ElementType;
                        }

                        if (operation.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                        {
                            return GetIEnumerableTParam(operation.Type);
                        }

                        INamedTypeSymbol? enumerableInterface = null;
                        foreach (var t in operation.Type.AllInterfaces)
                        {
                            if (t.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                            {
                                if (enumerableInterface is not null)
                                {
                                    return null; // if the type implements IEnumerable<T> multiple times, give up
                                }

                                enumerableInterface = t;
                            }
                        }

                        if (enumerableInterface is not null)
                        {
                            return GetIEnumerableTParam(enumerableInterface);
                        }

                        if (operation is IParenthesizedOperation parenthesizedOperation)
                        {
                            return FindElementType(parenthesizedOperation.Operand);
                        }

                        if (operation is IConversionOperation conversionOperation
                            && conversionOperation.OperatorMethod is null) // implicit meaning 'not user defined'
                        {
                            return FindElementType(conversionOperation.Operand);
                        }

                        return null;
                    }

                    // because the type of the parameter is actually the non-generic IEnumerable,
                    // we have to reach back through conversion operator(s) to get the element type
                    var castFrom = FindElementType(instanceArg);
                    if (castFrom is null)
                    {
                        return;
                    }

                    if (!invocation.TargetMethod.TypeArguments.HasExactly(1))
                    {
                        return;
                    }

                    var castTo = invocation.TargetMethod.TypeArguments[0];

                    if (CastWillAlwaysFail(castFrom, castTo))
                    {
                        context.ReportDiagnostic(invocation.CreateDiagnostic(rule, castFrom.ToDisplayString(), castTo.ToDisplayString()));
                    }
                }, OperationKind.Invocation);
            });

            // because this is a warning, we want to be very sure
            // this won't catch all problems, but it should never report something 
            // as a problem in correctly. We don't want another IDE0004
            static bool CastWillAlwaysFail(ITypeSymbol castFrom, ITypeSymbol castTo)
            {
                castFrom = castFrom.GetNullableValueTypeUnderlyingType()
                    ?? castFrom.GetUnderlyingValueTupleTypeOrThis()!;
                castTo = castTo.GetNullableValueTypeUnderlyingType()
                    ?? castTo.GetUnderlyingValueTupleTypeOrThis()!;

                if (castFrom.TypeKind == TypeKind.Error
                   || castTo.TypeKind == TypeKind.Error)
                {
                    return false;
                }

                // Most checks are better with OriginalDefinition, but keep the ones passed in around.
                ITypeSymbol castFromParam = castFrom;
                ITypeSymbol castToParam = castTo;

                castFrom = castFrom.OriginalDefinition;
                castTo = castTo.OriginalDefinition;

                if (castFrom.SpecialType == SpecialType.System_Object
                   || castTo.SpecialType == SpecialType.System_Object)
                {
                    // some things will actually fail, eg. TypedReference
                    // but they should be pretty rare
                    return false;
                }

                if (castFrom.Equals(castTo, SymbolEqualityComparer.Default))
                {
                    return false;
                }

                static bool IsUnconstrainedTypeParameter(ITypeParameterSymbol typeParameterSymbol)
                    => !typeParameterSymbol.HasValueTypeConstraint
                    && typeParameterSymbol.ConstraintTypes.IsEmpty;
                // because object is a reference type the 'class' reference type constraint
                // doesn't actually constrain unless a type is specified too
                // not implemented:
                //   NotNullConstraint
                //   ConstructorConstraint
                //   UnmanagedTypeConstraint
                //   Nullability annotations

                switch (castFrom.TypeKind, castTo.TypeKind)
                {
                    case (TypeKind.Dynamic, _):
                    case (_, TypeKind.Dynamic):
                        return false;

                    case (TypeKind.TypeParameter, _):
                        var castFromTypeParam = (ITypeParameterSymbol)castFrom;
                        if (IsUnconstrainedTypeParameter(castFromTypeParam))
                        {
                            return false;
                        }

                        if (castFromTypeParam.ConstraintTypes.Any(constraintType => CastWillAlwaysFail(constraintType, castTo)))
                        {
                            return true;
                        }

                        if (castFromTypeParam.HasValueTypeConstraint
                            && castTo.TypeKind == TypeKind.Class)
                        {
                            return true;
                        }

                        return false;
                    case (_, TypeKind.TypeParameter):
                        var castToTypeParam = (ITypeParameterSymbol)castTo;
                        if (IsUnconstrainedTypeParameter(castToTypeParam))
                        {
                            return false;
                        }

                        if (castToTypeParam.ConstraintTypes.Any(constraintType => CastWillAlwaysFail(castFrom, constraintType)))
                        {
                            return true;
                        }

                        if (castToTypeParam.HasValueTypeConstraint
                            && castFrom.TypeKind == TypeKind.Class)
                        {
                            return true;
                        }

                        return false;

                    case (TypeKind.Class, TypeKind.Class):
                        return !castFromParam.DerivesFrom(castToParam)
                            && !castToParam.DerivesFrom(castFromParam);

                    case (TypeKind.Interface, TypeKind.Class):
                        return castTo.IsSealed && !castTo.DerivesFrom(castFrom);
                    case (TypeKind.Class, TypeKind.Interface):
                        return castFrom.IsSealed && !castFrom.DerivesFrom(castTo);

                    case (TypeKind.Interface, TypeKind.Struct):
                        return !castTo.DerivesFrom(castFrom);
                    case (TypeKind.Struct, TypeKind.Interface):
                        return !castFrom.DerivesFrom(castTo);

                    case (TypeKind.Class, TypeKind.Enum):
                        return castFrom.SpecialType is not SpecialType.System_Enum
                                                   and not SpecialType.System_ValueType;
                    case (TypeKind.Enum, TypeKind.Class):
                        return castTo.SpecialType is not SpecialType.System_Enum
                                                 and not SpecialType.System_ValueType;

                    case (TypeKind.Struct, TypeKind.Enum)
                    when castTo is INamedTypeSymbol toEnum:
                        return !castFrom.Equals(toEnum.EnumUnderlyingType);
                    case (TypeKind.Enum, TypeKind.Struct)
                    when castFrom is INamedTypeSymbol fromEnum:
                        return !fromEnum.EnumUnderlyingType!.Equals(castTo);

                    case (TypeKind.Enum, TypeKind.Enum)
                    when castFrom is INamedTypeSymbol fromEnum
                      && castTo is INamedTypeSymbol toEnum:
                        return !fromEnum.EnumUnderlyingType!.Equals(toEnum.EnumUnderlyingType);

                    // this is too conservative
                    // array variance is not implemented
                    // - eg. object[] -> class[]
                    // boxing shouldn't be allowed
                    // - eg.  object[] -> ValueType[]
                    case (TypeKind.Array, TypeKind.Array)
                    when castFrom is IArrayTypeSymbol fromArray
                      && castTo is IArrayTypeSymbol toArray:
                        return fromArray.Rank != toArray.Rank
                            || CastWillAlwaysFail(fromArray.ElementType, toArray.ElementType);

                    case (TypeKind.Array, TypeKind.Class):
                        return castTo.SpecialType != SpecialType.System_Array;
                    case (TypeKind.Class, TypeKind.Array):
                        return castFrom.SpecialType != SpecialType.System_Array;

                    case (TypeKind.Class, TypeKind.Struct):
                        return castFrom.SpecialType != SpecialType.System_ValueType;
                    case (TypeKind.Struct, TypeKind.Class):
                        return castTo.SpecialType != SpecialType.System_ValueType;

                    case (_, TypeKind.Enum):
                    case (TypeKind.Enum, _):
                    case (_, TypeKind.Struct):
                    case (TypeKind.Struct, _):
                        return true;

                    case (TypeKind.Interface, TypeKind.Interface):
                    default:
                        return false; // we don't *know* it'll fail...
                }
            }
        }
    }
}
