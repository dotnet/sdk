// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1305: Specify IFormatProvider
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SpecifyIFormatProviderAnalyzer : AbstractGlobalizationDiagnosticAnalyzer
    {
        internal const string RuleId = "CA1305";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(SpecifyIFormatProviderTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(SpecifyIFormatProviderDescription));

        internal static readonly DiagnosticDescriptor IFormatProviderAlternateStringRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(SpecifyIFormatProviderMessageIFormatProviderAlternateString)),
            DiagnosticCategory.Globalization,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor IFormatProviderAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(SpecifyIFormatProviderMessageIFormatProviderAlternate)),
            DiagnosticCategory.Globalization,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor IFormatProviderOptionalRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(SpecifyIFormatProviderMessageIFormatProviderOptional)),
            DiagnosticCategory.Globalization,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UICultureStringRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(SpecifyIFormatProviderMessageUICultureString)),
            DiagnosticCategory.Globalization,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UICultureRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(SpecifyIFormatProviderMessageUICulture)),
            DiagnosticCategory.Globalization,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        private static readonly ImmutableArray<string> s_dateInvariantFormats = ImmutableArray.Create("o", "O", "r", "R", "s", "u");

        private static readonly ImmutableArray<SpecialType> s_numberTypes = ImmutableArray.Create(
            SpecialType.System_Int32,
            SpecialType.System_UInt32,
            SpecialType.System_Int64,
            SpecialType.System_UInt64,
            SpecialType.System_Int16,
            SpecialType.System_UInt16,
            SpecialType.System_Double,
            SpecialType.System_Single,
            SpecialType.System_Decimal);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(IFormatProviderAlternateStringRule, IFormatProviderAlternateRule, IFormatProviderOptionalRule, UICultureStringRule, UICultureRule);

        protected override void InitializeWorker(CompilationStartAnalysisContext context)
        {

            #region "Get All the WellKnown Types and Members"

            var typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
            var iformatProviderType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIFormatProvider);
            var cultureInfoType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGlobalizationCultureInfo);
            if (iformatProviderType == null || cultureInfoType == null)
            {
                return;
            }

            var objectType = context.Compilation.GetSpecialType(SpecialType.System_Object);
            var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);

            var charType = context.Compilation.GetSpecialType(SpecialType.System_Char);
            var boolType = context.Compilation.GetSpecialType(SpecialType.System_Boolean);
            var guidType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGuid);
            var numberTypes = s_numberTypes.Select(context.Compilation.GetSpecialType).ToImmutableArray();

            var nullableT = context.Compilation.GetSpecialType(SpecialType.System_Nullable_T);
            var invariantToStringMethodsBuilder = ImmutableHashSet.CreateBuilder<IMethodSymbol>();
            AddValidToStringMethods(invariantToStringMethodsBuilder, nullableT, charType);
            AddValidToStringMethods(invariantToStringMethodsBuilder, nullableT, boolType);
            AddValidToStringMethods(invariantToStringMethodsBuilder, nullableT, stringType);
            AddValidToStringMethods(invariantToStringMethodsBuilder, nullableT, guidType);
            var invariantToStringMethods = invariantToStringMethodsBuilder.ToImmutable();

            var dateTimeToStringFormatMethod = GetToStringWithFormatStringParameter(typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDateTime));

            var dateTimeOffsetToStringFormatMethod = GetToStringWithFormatStringParameter(typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDateTimeOffset));

            var timeSpanToStringFormatMethod = GetToStringWithFormatStringParameter(typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTimeSpan));

            var stringFormatMembers = stringType.GetMembers("Format").OfType<IMethodSymbol>();

            var stringFormatMemberWithStringAndObjectParameter = stringFormatMembers.GetFirstOrDefaultMemberWithParameterInfos(
                                                                     GetParameterInfo(stringType),
                                                                     GetParameterInfo(objectType));
            var stringFormatMemberWithStringObjectAndObjectParameter = stringFormatMembers.GetFirstOrDefaultMemberWithParameterInfos(
                                                                           GetParameterInfo(stringType),
                                                                           GetParameterInfo(objectType),
                                                                           GetParameterInfo(objectType));
            var stringFormatMemberWithStringObjectObjectAndObjectParameter = stringFormatMembers.GetFirstOrDefaultMemberWithParameterInfos(
                                                                                 GetParameterInfo(stringType),
                                                                                 GetParameterInfo(objectType),
                                                                                 GetParameterInfo(objectType),
                                                                                 GetParameterInfo(objectType));
            var stringFormatMemberWithStringAndParamsObjectParameter = stringFormatMembers.GetFirstOrDefaultMemberWithParameterInfos(
                                                                           GetParameterInfo(stringType),
                                                                           GetParameterInfo(objectType, isArray: true, arrayRank: 1, isParams: true));
            var stringFormatMemberWithIFormatProviderStringAndParamsObjectParameter = stringFormatMembers.GetFirstOrDefaultMemberWithParameterInfos(
                                                                                          GetParameterInfo(iformatProviderType),
                                                                                          GetParameterInfo(stringType),
                                                                                          GetParameterInfo(objectType, isArray: true, arrayRank: 1, isParams: true));

            var currentCultureProperty = cultureInfoType.GetMembers("CurrentCulture").OfType<IPropertySymbol>().FirstOrDefault();
            var invariantCultureProperty = cultureInfoType.GetMembers("InvariantCulture").OfType<IPropertySymbol>().FirstOrDefault();
            var currentUICultureProperty = cultureInfoType.GetMembers("CurrentUICulture").OfType<IPropertySymbol>().FirstOrDefault();
            var installedUICultureProperty = cultureInfoType.GetMembers("InstalledUICulture").OfType<IPropertySymbol>().FirstOrDefault();

            var threadType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread);
            var currentThreadCurrentUICultureProperty = threadType?.GetMembers("CurrentUICulture").OfType<IPropertySymbol>().FirstOrDefault();

            var activatorType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemActivator);
            var resourceManagerType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemResourcesResourceManager);

            var computerInfoType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualBasicDevicesComputerInfo);
            var installedUICulturePropertyOfComputerInfoType = computerInfoType?.GetMembers("InstalledUICulture").OfType<IPropertySymbol>().FirstOrDefault();

            var obsoleteAttributeType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);

            var guidParseMethods = guidType?.GetMembers("Parse") ?? ImmutableArray<ISymbol>.Empty;

            #endregion

            context.RegisterOperationAction(oaContext =>
            {
                var invocationExpression = (IInvocationOperation)oaContext.Operation;
                var targetMethod = invocationExpression.TargetMethod;

                #region "Exceptions"
                if (targetMethod.IsGenericMethod ||
                targetMethod.ContainingType.IsErrorType() ||
                (activatorType != null && activatorType.Equals(targetMethod.ContainingType)) ||
                (resourceManagerType != null && resourceManagerType.Equals(targetMethod.ContainingType)) ||
                IsValidToStringCall(invocationExpression, invariantToStringMethods, dateTimeToStringFormatMethod, dateTimeOffsetToStringFormatMethod, timeSpanToStringFormatMethod) ||
                IsValidParseCall(invocationExpression, guidParseMethods))
                {
                    return;
                }
                #endregion

                #region "IFormatProviderAlternateStringRule Only"
                if (stringFormatMemberWithIFormatProviderStringAndParamsObjectParameter != null &&
                    !oaContext.Options.IsConfiguredToSkipAnalysis(IFormatProviderAlternateStringRule, targetMethod, oaContext.ContainingSymbol, oaContext.Compilation) &&
                    (targetMethod.Equals(stringFormatMemberWithStringAndObjectParameter) ||
                     targetMethod.Equals(stringFormatMemberWithStringObjectAndObjectParameter) ||
                     targetMethod.Equals(stringFormatMemberWithStringObjectObjectAndObjectParameter) ||
                     targetMethod.Equals(stringFormatMemberWithStringAndParamsObjectParameter)))
                {
                    // Sample message for IFormatProviderAlternateStringRule: Because the behavior of string.Format(string, object) could vary based on the current user's locale settings,
                    // replace this call in IFormatProviderStringTest.M() with a call to string.Format(IFormatProvider, string, params object[]).
                    oaContext.ReportDiagnostic(
                    invocationExpression.Syntax.CreateDiagnostic(
                        IFormatProviderAlternateStringRule,
                        targetMethod.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                        oaContext.ContainingSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                        stringFormatMemberWithIFormatProviderStringAndParamsObjectParameter.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));

                    return;
                }
                #endregion

                #region "IFormatProviderAlternateStringRule & IFormatProviderAlternateRule"
                var iformatProviderAlternateRule = targetMethod.ReturnType.Equals(stringType) ?
                    IFormatProviderAlternateStringRule :
                    IFormatProviderAlternateRule;

                if (!oaContext.Options.IsConfiguredToSkipAnalysis(iformatProviderAlternateRule, targetMethod, oaContext.ContainingSymbol, oaContext.Compilation))
                {
                    bool diagnosticReported = false;
                    IEnumerable<IMethodSymbol> methodsWithSameNameAsTargetMethod = targetMethod.ContainingType.GetMembers(targetMethod.Name).OfType<IMethodSymbol>().WhereMethodDoesNotContainAttribute(obsoleteAttributeType);
                    if (methodsWithSameNameAsTargetMethod.HasMoreThan(1))
                    {
                        var correctOverloads = methodsWithSameNameAsTargetMethod.GetMethodOverloadsWithDesiredParameterAtLeadingOrTrailing(targetMethod, iformatProviderType);

                        // If there are two matching overloads, one with CultureInfo as the first parameter and one with CultureInfo as the last parameter,
                        // report the diagnostic on the overload with CultureInfo as the last parameter, to match the behavior of FxCop.
                        var correctOverload = correctOverloads.FirstOrDefault(overload => overload.Parameters.Last().Type.Equals(iformatProviderType)) ?? correctOverloads.FirstOrDefault();

                        // Sample message for IFormatProviderAlternateRule: Because the behavior of Convert.ToInt64(string) could vary based on the current user's locale settings,
                        // replace this call in IFormatProviderStringTest.TestMethod() with a call to Convert.ToInt64(string, IFormatProvider).
                        if (correctOverload != null)
                        {
                            oaContext.ReportDiagnostic(
                                invocationExpression.Syntax.CreateDiagnostic(
                                    iformatProviderAlternateRule,
                                    targetMethod.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                    oaContext.ContainingSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                    correctOverload.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                            diagnosticReported = true;
                        }
                    }

                    // If we haven't found any overload with an extra parameter of type IFormatProvider or if the method doesn't have any overload,
                    // we still want to check if the target method is not accepting an optional IFormatProvider, in which case we should report the
                    // diagnostic.
                    if (!diagnosticReported)
                    {
                        var currentCallHasNullFormatProvider = invocationExpression.Arguments.Any(x =>
                            SymbolEqualityComparer.Default.Equals(x.Parameter?.Type, iformatProviderType)
                            && x.ArgumentKind == ArgumentKind.DefaultValue);

                        var nullableType = invocationExpression.Instance?.Type.GetNullableValueTypeUnderlyingType();
                        var isDefaultToStringInvocation = invocationExpression.TargetMethod is { Name: nameof(object.ToString), Parameters.Length: 0 };
                        var isNullableNumberToStringInvocation = isDefaultToStringInvocation && numberTypes.Contains(nullableType, SymbolEqualityComparer.Default);

                        if (currentCallHasNullFormatProvider || isNullableNumberToStringInvocation)
                        {
                            oaContext.ReportDiagnostic(invocationExpression.CreateDiagnostic(IFormatProviderOptionalRule,
                                targetMethod.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                            diagnosticReported = true;
                        }
                    }
                }
                #endregion

                #region "UICultureStringRule & UICultureRule"
                var uiCultureRule = targetMethod.ReturnType.Equals(stringType) ?
                    UICultureStringRule :
                    UICultureRule;

                if (!oaContext.Options.IsConfiguredToSkipAnalysis(uiCultureRule, targetMethod, oaContext.ContainingSymbol, oaContext.Compilation))
                {
                    IEnumerable<int> IformatProviderParameterIndices = GetIndexesOfParameterType(targetMethod, iformatProviderType);
                    foreach (var index in IformatProviderParameterIndices)
                    {
                        var argument = invocationExpression.Arguments[index];

                        if (argument != null && currentUICultureProperty != null &&
                            installedUICultureProperty != null && currentThreadCurrentUICultureProperty != null)
                        {
                            var semanticModel = argument.SemanticModel!;

                            var symbol = semanticModel.GetSymbolInfo(argument.Value.Syntax, oaContext.CancellationToken).Symbol;

                            if (symbol != null &&
                                (symbol.Equals(currentUICultureProperty) ||
                                    symbol.Equals(installedUICultureProperty) ||
                                    symbol.Equals(currentThreadCurrentUICultureProperty) ||
                                    (installedUICulturePropertyOfComputerInfoType != null && symbol.Equals(installedUICulturePropertyOfComputerInfoType))))
                            {
                                // Sample message
                                // 1. UICultureStringRule - 'TestClass.TestMethod()' passes 'Thread.CurrentUICulture' as the 'IFormatProvider' parameter to 'TestClass.CalleeMethod(string, IFormatProvider)'.
                                // This property returns a culture that is inappropriate for formatting methods.
                                // 2. UICultureRule -'TestClass.TestMethod()' passes 'CultureInfo.CurrentUICulture' as the 'IFormatProvider' parameter to 'TestClass.Callee(IFormatProvider, string)'.
                                // This property returns a culture that is inappropriate for formatting methods.

                                oaContext.ReportDiagnostic(
                                invocationExpression.Syntax.CreateDiagnostic(
                                    uiCultureRule,
                                    oaContext.ContainingSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                    symbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                    targetMethod.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                            }
                        }
                    }
                }
                #endregion

            }, OperationKind.Invocation);
        }

        private static IMethodSymbol? GetToStringWithFormatStringParameter(INamedTypeSymbol? type)
        {
            return type?.GetMembers("ToString").OfType<IMethodSymbol>().FirstOrDefault(s => s.Parameters is [{ Type.SpecialType: SpecialType.System_String }]);
        }

        private static void AddValidToStringMethods(ImmutableHashSet<IMethodSymbol>.Builder validToStringMethodsBuilder, INamedTypeSymbol nullableT, INamedTypeSymbol? type)
        {
            if (type is null)
            {
                return;
            }

            validToStringMethodsBuilder.AddRange(GetToStringMethods(type));
            validToStringMethodsBuilder.AddRange(GetToStringMethods(nullableT.Construct(type)));

            static IEnumerable<IMethodSymbol> GetToStringMethods(INamedTypeSymbol namedTypeSymbol)
                => namedTypeSymbol.GetMembers("ToString").OfType<IMethodSymbol>().WhereNotNull();
        }

        private static IEnumerable<int> GetIndexesOfParameterType(IMethodSymbol targetMethod, INamedTypeSymbol formatProviderType)
        {
            return targetMethod.Parameters
                .Select((Parameter, Index) => (Parameter, Index))
                .Where(x => x.Parameter.Type.Equals(formatProviderType))
                .Select(x => x.Index);
        }

        private static ParameterInfo GetParameterInfo(INamedTypeSymbol type, bool isArray = false, int arrayRank = 0, bool isParams = false)
        {
            return ParameterInfo.GetParameterInfo(type, isArray, arrayRank, isParams);
        }

        private static bool IsValidToStringCall(IInvocationOperation invocationOperation, ImmutableHashSet<IMethodSymbol> validToStringMethods,
            IMethodSymbol? dateTimeToStringFormatMethod, IMethodSymbol? dateTimeOffsetToStringFormatMethod, IMethodSymbol? timeSpanToStringFormatMethod)
        {
            var targetMethod = invocationOperation.TargetMethod;
            if (validToStringMethods.Contains(targetMethod))
            {
                return true;
            }

            if (invocationOperation.Arguments.Length != 1 ||
                !invocationOperation.Arguments[0].Value.ConstantValue.HasValue ||
                invocationOperation.Arguments[0].Value.ConstantValue.Value is not string format)
            {
                return false;
            }

            // Handle invariant format specifiers, see https://github.com/dotnet/roslyn-analyzers/issues/3507
            if (targetMethod.Equals(dateTimeToStringFormatMethod, SymbolEqualityComparer.Default) || targetMethod.Equals(dateTimeOffsetToStringFormatMethod, SymbolEqualityComparer.Default))
            {
                return s_dateInvariantFormats.Contains(format);
            }

            if (targetMethod.Equals(timeSpanToStringFormatMethod, SymbolEqualityComparer.Default))
            {
                return format == "c";
            }

            return false;
        }

        private static bool IsValidParseCall(IInvocationOperation invocationOperation, ImmutableArray<ISymbol> guidParseMethods)
            => guidParseMethods.Contains(invocationOperation.TargetMethod);
    }
}
