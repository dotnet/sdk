// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA1308: Normalize strings to uppercase
    /// <para>
    /// Strings should be normalized to uppercase. A small group of characters, when they are converted to lowercase, cannot make a round trip.
    /// To make a round trip means to convert the characters from one locale to another locale that represents character data differently,
    /// and then to accurately retrieve the original characters from the converted characters.
    /// </para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class NormalizeStringsToUppercaseAnalyzer : AbstractGlobalizationDiagnosticAnalyzer
    {
        internal const string RuleId = "CA1308";

        internal static readonly DiagnosticDescriptor ToUpperRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(NormalizeStringsToUppercaseTitle)),
            CreateLocalizableResourceString(nameof(NormalizeStringsToUppercaseMessageToUpper)),
            DiagnosticCategory.Globalization,
            RuleLevel.CandidateForRemoval,
            description: CreateLocalizableResourceString(nameof(NormalizeStringsToUppercaseDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(ToUpperRule);

        protected override void InitializeWorker(CompilationStartAnalysisContext context)
        {
            var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);

            var cultureInfo = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGlobalizationCultureInfo);
            var invariantCulture = cultureInfo?.GetMembers("InvariantCulture").OfType<IPropertySymbol>().FirstOrDefault();

            // We want to flag calls to "ToLowerInvariant" and "ToLower(CultureInfo.InvariantCulture)".
            var toLowerInvariant = stringType.GetMembers("ToLowerInvariant").OfType<IMethodSymbol>().FirstOrDefault();
            var toLowerWithCultureInfo = cultureInfo != null ?
                stringType.GetMembers("ToLower").OfType<IMethodSymbol>().FirstOrDefault(m => m.Parameters.Length == 1 && Equals(m.Parameters[0].Type, cultureInfo)) :
                null;

            if (toLowerInvariant == null && toLowerWithCultureInfo == null)
            {
                return;
            }

            // We want to recommend calling "ToUpperInvariant" or "ToUpper(CultureInfo.InvariantCulture)".
            var toUpperInvariant = stringType.GetMembers("ToUpperInvariant").OfType<IMethodSymbol>().FirstOrDefault();
            var toUpperWithCultureInfo = cultureInfo != null ?
                stringType.GetMembers("ToUpper").OfType<IMethodSymbol>().FirstOrDefault(m => m.Parameters.Length == 1 && Equals(m.Parameters[0].Type, cultureInfo)) :
                null;

            if (toUpperInvariant == null && toUpperWithCultureInfo == null)
            {
                return;
            }

            context.RegisterOperationAction(operationAnalysisContext =>
            {
                var invocation = (IInvocationOperation)operationAnalysisContext.Operation;
                if (invocation.TargetMethod == null)
                {
                    return;
                }

                var method = invocation.TargetMethod;
                if (method.Equals(toLowerInvariant) ||
                    (method.Equals(toLowerWithCultureInfo) &&
                     ((invocation.Arguments.FirstOrDefault()?.Value as IMemberReferenceOperation)?.Member.Equals(invariantCulture) ?? false)))
                {
                    IMethodSymbol suggestedMethod = toUpperInvariant ?? toUpperWithCultureInfo!;

                    // In method {0}, replace the call to {1} with {2}.
                    var diagnostic = invocation.CreateDiagnostic(ToUpperRule, operationAnalysisContext.ContainingSymbol.Name, method.Name, suggestedMethod.Name);
                    operationAnalysisContext.ReportDiagnostic(diagnostic);
                }
            }, OperationKind.Invocation);
        }
    }
}