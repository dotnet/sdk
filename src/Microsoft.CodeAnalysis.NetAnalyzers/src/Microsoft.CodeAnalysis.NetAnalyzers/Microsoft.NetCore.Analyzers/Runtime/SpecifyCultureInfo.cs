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
    /// CA1304: Specify CultureInfo
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SpecifyCultureInfoAnalyzer : AbstractGlobalizationDiagnosticAnalyzer
    {
        internal const string RuleId = "CA1304";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(SpecifyCultureInfoTitle)),
            CreateLocalizableResourceString(nameof(SpecifyCultureInfoMessage)),
            DiagnosticCategory.Globalization,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(SpecifyCultureInfoDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        protected override void InitializeWorker(CompilationStartAnalysisContext context)
        {
            var obsoleteAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute);

            var cultureInfoType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGlobalizationCultureInfo);
            if (cultureInfoType != null)
            {
                context.RegisterOperationAction(oaContext =>
                {
                    var invocationExpression = (IInvocationOperation)oaContext.Operation;
                    var targetMethod = invocationExpression.TargetMethod;
                    if (targetMethod.ContainingType == null || targetMethod.ContainingType.IsErrorType() || targetMethod.IsGenericMethod)
                    {
                        return;
                    }

                    if (oaContext.Options.IsConfiguredToSkipAnalysis(Rule, targetMethod, oaContext.ContainingSymbol, oaContext.Compilation))
                    {
                        return;
                    }

                    IEnumerable<IMethodSymbol> methodsWithSameNameAsTargetMethod = targetMethod.ContainingType.GetMembers(targetMethod.Name).OfType<IMethodSymbol>().WhereMethodDoesNotContainAttribute(obsoleteAttributeType).ToList();
                    if (methodsWithSameNameAsTargetMethod.HasFewerThan(2))
                    {
                        return;
                    }

                    var correctOverloads = methodsWithSameNameAsTargetMethod.GetMethodOverloadsWithDesiredParameterAtLeadingOrTrailing(targetMethod, cultureInfoType).ToList();

                    // If there are two matching overloads, one with CultureInfo as the first parameter and one with CultureInfo as the last parameter,
                    // report the diagnostic on the overload with CultureInfo as the last parameter, to match the behavior of FxCop.
                    var correctOverload = correctOverloads.FirstOrDefault(overload => overload.Parameters.Last().Type.Equals(cultureInfoType)) ?? correctOverloads.FirstOrDefault();

                    if (correctOverload != null)
                    {
                        oaContext.ReportDiagnostic(
                            invocationExpression.Syntax.CreateDiagnostic(
                                Rule,
                                targetMethod.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                oaContext.ContainingSymbol.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat),
                                correctOverload.ToDisplayString(SymbolDisplayFormats.ShortSymbolDisplayFormat)));
                    }
                }, OperationKind.Invocation);
            }
        }
    }
}