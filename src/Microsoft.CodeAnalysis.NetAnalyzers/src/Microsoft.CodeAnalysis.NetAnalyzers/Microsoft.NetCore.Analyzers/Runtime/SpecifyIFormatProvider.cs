// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
    /// <summary>
    /// CA1305: Specify IFormatProvider
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SpecifyIFormatProviderAnalyzer : AbstractGlobalizationDiagnosticAnalyzer
    {
        internal const string RuleId = "CA1305";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyIFormatProviderTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageIFormatProviderAlternateString = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyIFormatProviderMessageIFormatProviderAlternateString), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageIFormatProviderAlternate = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyIFormatProviderMessageIFormatProviderAlternate), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageUICultureString = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyIFormatProviderMessageUICultureString), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageUICulture = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyIFormatProviderMessageUICulture), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyIFormatProviderDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor IFormatProviderAlternateStringRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageIFormatProviderAlternateString,
                                                                             DiagnosticCategory.Globalization,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor IFormatProviderAlternateRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageIFormatProviderAlternate,
                                                                             DiagnosticCategory.Globalization,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor UICultureStringRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageUICultureString,
                                                                             DiagnosticCategory.Globalization,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor UICultureRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageUICulture,
                                                                             DiagnosticCategory.Globalization,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        private static readonly ImmutableArray<string> s_dateInvariantFormats = ImmutableArray.Create("o", "O", "r", "R", "s", "u");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(IFormatProviderAlternateStringRule, IFormatProviderAlternateRule, UICultureStringRule, UICultureRule);

        protected override void InitializeWorker(CompilationStartAnalysisContext context)
        {

            #region "Get All the WellKnown Types and Members"
            var iformatProviderType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIFormatProvider);
            var cultureInfoType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGlobalizationCultureInfo);
            if (iformatProviderType == null || cultureInfoType == null)
            {
                return;
            }

            var objectType = context.Compilation.GetSpecialType(SpecialType.System_Object);
            var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
            if (objectType == null || stringType == null)
            {
                return;
            }

            var charType = context.Compilation.GetSpecialType(SpecialType.System_Char);
            var boolType = context.Compilation.GetSpecialType(SpecialType.System_Boolean);
            var guidType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGuid);

            var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();
            builder.AddIfNotNull(charType);
            builder.AddIfNotNull(boolType);
            builder.AddIfNotNull(stringType);
            builder.AddIfNotNull(guidType);
            var invariantToStringTypes = builder.ToImmutableHashSet();

            var dateTimeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDateTime);
            var dateTimeOffsetType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDateTimeOffset);
            var timeSpanType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTimeSpan);

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

            var threadType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingThread);
            var currentThreadCurrentUICultureProperty = threadType?.GetMembers("CurrentUICulture").OfType<IPropertySymbol>().FirstOrDefault();

            var activatorType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemActivator);
            var resourceManagerType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemResourcesResourceManager);

            var computerInfoType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualBasicDevicesComputerInfo);
            var installedUICulturePropertyOfComputerInfoType = computerInfoType?.GetMembers("InstalledUICulture").OfType<IPropertySymbol>().FirstOrDefault();

            var obsoleteAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);
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
                IsValidToStringCall(invocationExpression, invariantToStringTypes, dateTimeType, dateTimeOffsetType, timeSpanType))
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
                {
                    var rule = targetMethod.ReturnType.Equals(stringType) ?
                        IFormatProviderAlternateStringRule :
                        IFormatProviderAlternateRule;

                    if (!oaContext.Options.IsConfiguredToSkipAnalysis(rule, targetMethod, oaContext.ContainingSymbol, oaContext.Compilation))
                    {
                        IEnumerable<IMethodSymbol> methodsWithSameNameAsTargetMethod = targetMethod.ContainingType.GetMembers(targetMethod.Name).OfType<IMethodSymbol>().WhereMethodDoesNotContainAttribute(obsoleteAttributeType).ToList();
                        if (methodsWithSameNameAsTargetMethod.HasMoreThan(1))
                        {
                            var correctOverloads = methodsWithSameNameAsTargetMethod.GetMethodOverloadsWithDesiredParameterAtLeadingOrTrailing(targetMethod, iformatProviderType).ToList();

                            // If there are two matching overloads, one with CultureInfo as the first parameter and one with CultureInfo as the last parameter,
                            // report the diagnostic on the overload with CultureInfo as the last parameter, to match the behavior of FxCop.
                            var correctOverload = correctOverloads.FirstOrDefault(overload => overload.Parameters.Last().Type.Equals(iformatProviderType)) ?? correctOverloads.FirstOrDefault();

                            // Sample message for IFormatProviderAlternateRule: Because the behavior of Convert.ToInt64(string) could vary based on the current user's locale settings,
                            // replace this call in IFormatProviderStringTest.TestMethod() with a call to Convert.ToInt64(string, IFormatProvider).
                            if (correctOverload != null)
                            {
                                oaContext.ReportDiagnostic(
                                    invocationExpression.Syntax.CreateDiagnostic(
                                        rule,
                                        targetMethod.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                        oaContext.ContainingSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                        correctOverload.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                            }
                        }
                    }
                }
                #endregion

                #region "UICultureStringRule & UICultureRule"
                {
                    var rule = targetMethod.ReturnType.Equals(stringType) ?
                        UICultureStringRule :
                        UICultureRule;

                    if (!oaContext.Options.IsConfiguredToSkipAnalysis(rule, targetMethod, oaContext.ContainingSymbol, oaContext.Compilation))
                    {
                        IEnumerable<int> IformatProviderParameterIndices = GetIndexesOfParameterType(targetMethod, iformatProviderType);
                        foreach (var index in IformatProviderParameterIndices)
                        {
                            var argument = invocationExpression.Arguments[index];

                            if (argument != null && currentUICultureProperty != null &&
                                installedUICultureProperty != null && currentThreadCurrentUICultureProperty != null)
                            {
                                var semanticModel = argument.SemanticModel;

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
                                        rule,
                                        oaContext.ContainingSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                        symbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                        targetMethod.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                                }
                            }
                        }
                    }
                }
                #endregion

            }, OperationKind.Invocation);
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

        private static bool IsValidToStringCall(IInvocationOperation invocationOperation, ImmutableHashSet<INamedTypeSymbol> invariantToStringTypes,
            INamedTypeSymbol? dateTimeType, INamedTypeSymbol? dateTimeOffsetType, INamedTypeSymbol? timeSpanType)
        {
            var targetMethod = invocationOperation.TargetMethod;

            if (targetMethod.Name != "ToString")
            {
                return false;
            }

            if (invariantToStringTypes.Contains(UnwrapNullableValueTypes(targetMethod.ContainingType)))
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
            if ((dateTimeType != null && targetMethod.ContainingType.Equals(dateTimeType)) ||
                (dateTimeOffsetType != null && targetMethod.ContainingType.Equals(dateTimeOffsetType)))
            {
                return s_dateInvariantFormats.Contains(format);
            }

            if (timeSpanType != null && targetMethod.ContainingType.Equals(timeSpanType))
            {
                return format == "c";
            }

            return false;

            //  Local functions

            static INamedTypeSymbol UnwrapNullableValueTypes(INamedTypeSymbol typeSymbol)
            {
                if (typeSymbol.IsNullableValueType() && typeSymbol.TypeArguments[0] is INamedTypeSymbol nullableTypeArgument)
                    return nullableTypeArgument;
                return typeSymbol;
            }
        }
    }
}
