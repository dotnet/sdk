// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2013: Do not use ReferenceEquals with value types.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseReferenceEqualsWithValueTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2013";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseReferenceEqualsWithValueTypesTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableComparerMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseReferenceEqualsWithValueTypesComparerMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMethodMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseReferenceEqualsWithValueTypesMethodMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseReferenceEqualsWithValueTypesDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static readonly DiagnosticDescriptor ComparerRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            s_localizableComparerMessage,
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor MethodRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            s_localizableMethodMessage,
            DiagnosticCategory.Reliability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MethodRule, ComparerRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var objectType = compilationStartContext.Compilation.GetSpecialType(SpecialType.System_Object);

                if (objectType == null)
                {
                    return;
                }

                var objectObjectParameters = new[]
                {
                    ParameterInfo.GetParameterInfo(objectType),
                    ParameterInfo.GetParameterInfo(objectType)
                };

                var referenceEqualsMethodGroup = objectType.GetMembers("ReferenceEquals").OfType<IMethodSymbol>();
                var referenceEqualsMethod = referenceEqualsMethodGroup.GetFirstOrDefaultMemberWithParameterInfos(
                    objectObjectParameters);

                if (referenceEqualsMethod == null)
                {
                    return;
                }

                var typeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartContext.Compilation);
                var referenceEqualityComparer =
                    typeProvider.GetOrCreateTypeByMetadataName("System.Collections.Generic.ReferenceEqualityComparer");

                IMethodSymbol? comparerEqualsMethod = null;

                if (referenceEqualityComparer != null)
                {
                    var equalsMethodGroup = referenceEqualityComparer.GetMembers("Equals").OfType<IMethodSymbol>();
                    comparerEqualsMethod = equalsMethodGroup.GetFirstOrDefaultMemberWithParameterInfos(
                        objectObjectParameters);
                }

                compilationStartContext.RegisterOperationAction(operationContext =>
                {
                    var invocationExpression = (IInvocationOperation)operationContext.Operation;
                    var targetMethod = invocationExpression.TargetMethod;
                    DiagnosticDescriptor rule;

                    if (targetMethod == null)
                    {
                        return;
                    }

                    if (referenceEqualsMethod.Equals(targetMethod))
                    {
                        rule = MethodRule;
                    }
                    else if (comparerEqualsMethod != null && comparerEqualsMethod.Equals(targetMethod))
                    {
                        rule = ComparerRule;
                    }
                    else
                    {
                        return;
                    }

                    foreach (var argument in invocationExpression.Arguments)
                    {
                        var val = argument.Value;

                        // Only check through one level of conversion,
                        // which will be either the boxing conversion to object,
                        // or a reference type implicit conversion to object.
                        if (val is IConversionOperation conversion)
                        {
                            val = conversion.Operand;
                        }

                        if (val.Type?.IsValueType == true)
                        {
                            operationContext.ReportDiagnostic(
                                val.CreateDiagnostic(
                                    rule,
                                    val.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                        }
                    }
                },
                OperationKind.Invocation);
            });
        }
    }
}