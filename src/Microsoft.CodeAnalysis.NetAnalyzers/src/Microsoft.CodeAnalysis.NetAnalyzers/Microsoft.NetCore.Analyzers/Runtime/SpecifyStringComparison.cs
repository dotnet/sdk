// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1307: Specify StringComparison
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SpecifyStringComparisonAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1307";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyStringComparisonTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyStringComparisonMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.SpecifyStringComparisonDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Globalization,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1307-specify-stringcomparison",
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(csaContext =>
            {
                var stringComparisonType = csaContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison);
                var stringType = csaContext.Compilation.GetSpecialType(SpecialType.System_String);

                // Without these symbols the rule cannot run
                if (stringComparisonType == null || stringType == null)
                {
                    return;
                }

                var objectType = csaContext.Compilation.GetSpecialType(SpecialType.System_Object);
                var booleanType = csaContext.Compilation.GetSpecialType(SpecialType.System_Boolean);
                var integerType = csaContext.Compilation.GetSpecialType(SpecialType.System_Int32);
                var stringCompareToNamedMethods = stringType.GetMembers("CompareTo").OfType<IMethodSymbol>();
                var stringCompareToParameterString = stringCompareToNamedMethods.GetFirstOrDefaultMemberWithParameterInfos(
                                                         GetParameterInfo(stringType));
                var stringCompareToParameterObject = stringCompareToNamedMethods.GetFirstOrDefaultMemberWithParameterInfos(
                                                         GetParameterInfo(objectType));

                var stringCompareNamedMethods = stringType.GetMembers("Compare").OfType<IMethodSymbol>();
                var stringCompareParameterStringStringBool = stringCompareNamedMethods.GetFirstOrDefaultMemberWithParameterInfos(
                                                                 GetParameterInfo(stringType),
                                                                 GetParameterInfo(stringType),
                                                                 GetParameterInfo(booleanType));
                var stringCompareParameterStringStringStringComparison = stringCompareNamedMethods.GetFirstOrDefaultMemberWithParameterInfos(
                                                                             GetParameterInfo(stringType),
                                                                             GetParameterInfo(stringType),
                                                                             GetParameterInfo(stringComparisonType));
                var stringCompareParameterStringIntStringIntIntBool = stringCompareNamedMethods.GetFirstOrDefaultMemberWithParameterInfos(
                                                                          GetParameterInfo(stringType),
                                                                          GetParameterInfo(integerType),
                                                                          GetParameterInfo(stringType),
                                                                          GetParameterInfo(integerType),
                                                                          GetParameterInfo(integerType),
                                                                          GetParameterInfo(booleanType));
                var stringCompareParameterStringIntStringIntIntComparison = stringCompareNamedMethods.GetFirstOrDefaultMemberWithParameterInfos(
                                                                                GetParameterInfo(stringType),
                                                                                GetParameterInfo(integerType),
                                                                                GetParameterInfo(stringType),
                                                                                GetParameterInfo(integerType),
                                                                                GetParameterInfo(integerType),
                                                                                GetParameterInfo(stringComparisonType));

                var overloadMapBuilder = ImmutableDictionary.CreateBuilder<IMethodSymbol, IMethodSymbol>();
                overloadMapBuilder.AddKeyValueIfNotNull(stringCompareToParameterString, stringCompareParameterStringStringStringComparison);
                overloadMapBuilder.AddKeyValueIfNotNull(stringCompareToParameterObject, stringCompareParameterStringStringStringComparison);
                overloadMapBuilder.AddKeyValueIfNotNull(stringCompareParameterStringStringBool, stringCompareParameterStringStringStringComparison);
                overloadMapBuilder.AddKeyValueIfNotNull(stringCompareParameterStringIntStringIntIntBool, stringCompareParameterStringIntStringIntIntComparison);
                var overloadMap = overloadMapBuilder.ToImmutable();

                csaContext.RegisterOperationAction(oaContext =>
                {
                    var invocationExpression = (IInvocationOperation)oaContext.Operation;
                    var targetMethod = invocationExpression.TargetMethod;

                    if (targetMethod.IsGenericMethod ||
                        targetMethod.ContainingType == null ||
                        targetMethod.ContainingType.IsErrorType())
                    {
                        return;
                    }

                    if (overloadMap.Count != 0 && overloadMap.ContainsKey(targetMethod))
                    {
                        ReportDiagnostic(
                            oaContext,
                            invocationExpression,
                            targetMethod,
                            overloadMap[targetMethod]);

                        return;
                    }

                    IEnumerable<IMethodSymbol> methodsWithSameNameAsTargetMethod = targetMethod.ContainingType.GetMembers(targetMethod.Name).OfType<IMethodSymbol>();
                    if (methodsWithSameNameAsTargetMethod.HasMoreThan(1))
                    {
                        var correctOverload = methodsWithSameNameAsTargetMethod
                                                .GetMethodOverloadsWithDesiredParameterAtTrailing(targetMethod, stringComparisonType)
                                                .FirstOrDefault();

                        if (correctOverload != null)
                        {
                            ReportDiagnostic(
                                oaContext,
                                invocationExpression,
                                targetMethod,
                                correctOverload);
                        }
                    }
                }, OperationKind.Invocation);
            });
        }

        private static void ReportDiagnostic(
            OperationAnalysisContext oaContext,
            IInvocationOperation invocationExpression,
            IMethodSymbol targetMethod,
            IMethodSymbol correctOverload)
        {
            oaContext.ReportDiagnostic(
                invocationExpression.Syntax.CreateDiagnostic(
                    Rule,
                    targetMethod.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    oaContext.ContainingSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    correctOverload.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }

        private static ParameterInfo GetParameterInfo(INamedTypeSymbol type, bool isArray = false, int arrayRank = 0, bool isParams = false)
        {
            return ParameterInfo.GetParameterInfo(type, isArray, arrayRank, isParams);
        }
    }
}