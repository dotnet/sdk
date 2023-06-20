// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1862: Prefer the StringComparison method overloads to perform case-insensitive string comparisons.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class RecommendCaseInsensitiveStringComparisonAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1862";

        internal const string StringComparisonInvariantCultureIgnoreCaseName = "InvariantCultureIgnoreCase";
        internal const string StringComparisonCurrentCultureIgnoreCaseName = "CurrentCultureIgnoreCase";
        internal const string StringToLowerMethodName = "ToLower";
        internal const string StringToUpperMethodName = "ToUpper";
        internal const string StringToLowerInvariantMethodName = "ToLowerInvariant";
        internal const string StringToUpperInvariantMethodName = "ToUpperInvariant";
        internal const string StringContainsMethodName = "Contains";
        internal const string StringIndexOfMethodName = "IndexOf";
        internal const string StringStartsWithMethodName = "StartsWith";
        internal const string StringCompareToMethodName = "CompareTo";
        internal const string StringComparerCompareMethodName = "Compare";
        internal const string StringParameterName = "value";
        internal const string StringComparisonParameterName = "comparisonType";

        internal static readonly DiagnosticDescriptor RecommendCaseInsensitiveStringComparisonRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparisonTitle)),
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparisonMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparisonDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RecommendCaseInsensitiveStringComparerRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparerTitle)),
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparerMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparerDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            RecommendCaseInsensitiveStringComparisonRule, RecommendCaseInsensitiveStringComparerRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            // Retrieve the essential types: string, StringComparison, StringComparer

            INamedTypeSymbol stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
            INamedTypeSymbol int32Type = context.Compilation.GetSpecialType(SpecialType.System_Int32);

            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison, out INamedTypeSymbol? stringComparisonType))
            {
                return;
            }

            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparer, out INamedTypeSymbol? stringComparerType))
            {
                return;
            }

            // Retrieve the offending parameterless methods: ToLower, ToLowerInvariant, ToUpper, ToUpperInvariant

            IMethodSymbol? toLowerParameterlessMethod = stringType.GetMembers(StringToLowerMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos();
            if (toLowerParameterlessMethod == null)
            {
                return;
            }

            IMethodSymbol? toLowerInvariantParameterlessMethod = stringType.GetMembers(StringToLowerInvariantMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos();
            if (toLowerInvariantParameterlessMethod == null)
            {
                return;
            }

            IMethodSymbol? toUpperParameterlessMethod = stringType.GetMembers(StringToUpperMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos();
            if (toUpperParameterlessMethod == null)
            {
                return;
            }

            IMethodSymbol? toUpperInvariantParameterlessMethod = stringType.GetMembers(StringToUpperInvariantMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos();
            if (toUpperInvariantParameterlessMethod == null)
            {
                return;
            }

            // Create the different expected parameter combinations

            ParameterInfo[] stringParameter = new[]
            {
                ParameterInfo.GetParameterInfo(stringType)
            };

            ParameterInfo[] stringInt32Parameters = new[]
            {
                ParameterInfo.GetParameterInfo(stringType),
                ParameterInfo.GetParameterInfo(int32Type)
            };

            ParameterInfo[] stringInt32Int32Parameters = new[]
            {
                ParameterInfo.GetParameterInfo(stringType),
                ParameterInfo.GetParameterInfo(int32Type),
                ParameterInfo.GetParameterInfo(int32Type)
            };

            // Retrieve the diagnosable string overload methods: Contains, IndexOf (3 overloads), StartsWith, CompareTo

            // Contains(string)
            IMethodSymbol? containsStringMethod = stringType.GetMembers(StringContainsMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(stringParameter);
            if (containsStringMethod == null)
            {
                return;
            }

            // StartsWith(string)
            IMethodSymbol? startsWithStringMethod = stringType.GetMembers(StringStartsWithMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(stringParameter);
            if (startsWithStringMethod == null)
            {
                return;
            }

            IEnumerable<IMethodSymbol> indexOfMethods = stringType.GetMembers(StringIndexOfMethodName).OfType<IMethodSymbol>();

            // IndexOf(string)
            IMethodSymbol? indexOfStringMethod = indexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(stringParameter);
            if (indexOfStringMethod == null)
            {
                return;
            }

            // IndexOf(string, int startIndex)
            IMethodSymbol? indexOfStringInt32Method = indexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(stringInt32Parameters);
            if (indexOfStringInt32Method == null)
            {
                return;
            }

            // IndexOf(string, int startIndex, int count)
            IMethodSymbol? indexOfStringInt32Int32Method = indexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(stringInt32Int32Parameters);
            if (indexOfStringInt32Int32Method == null)
            {
                return;
            }

            // CompareTo(string)
            IMethodSymbol? compareToStringMethod = stringType.GetMembers(StringCompareToMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(stringParameter);
            if (compareToStringMethod == null)
            {
                return;
            }

            // Retrieve the StringComparer properties that need to be flagged: CurrentCultureIgnoreCase, InvariantCultureIgnoreCase

            IEnumerable<IPropertySymbol> ccicPropertyGroup = stringComparerType.GetMembers(StringComparisonCurrentCultureIgnoreCaseName).OfType<IPropertySymbol>();
            if (!ccicPropertyGroup.Any())
            {
                return;
            }

            IEnumerable<IPropertySymbol> icicPropertyGroup = stringComparerType.GetMembers(StringComparisonInvariantCultureIgnoreCaseName).OfType<IPropertySymbol>();
            if (!icicPropertyGroup.Any())
            {
                return;
            }

            context.RegisterOperationAction(context =>
            {
                IInvocationOperation caseChangingInvocation = (IInvocationOperation)context.Operation;
                IMethodSymbol caseChangingMethod = caseChangingInvocation.TargetMethod;

                if (!caseChangingMethod.Equals(toLowerParameterlessMethod) &&
                    !caseChangingMethod.Equals(toLowerInvariantParameterlessMethod) &&
                    !caseChangingMethod.Equals(toUpperParameterlessMethod) &&
                    !caseChangingMethod.Equals(toUpperInvariantParameterlessMethod))
                {
                    return;
                }

                // Ignore parenthesized operations
                IOperation? ancestor = caseChangingInvocation.WalkUpParentheses().WalkUpConversion().Parent;

                IInvocationOperation diagnosableInvocation;
                if (ancestor is IInvocationOperation invocationAncestor)
                {
                    diagnosableInvocation = invocationAncestor;
                }
                else if (ancestor is IArgumentOperation argumentAncestor && argumentAncestor.Parent is IInvocationOperation argumentInvocationAncestor)
                {
                    diagnosableInvocation = argumentInvocationAncestor;
                }
                else
                {
                    return;
                }

                IMethodSymbol diagnosableMethod = diagnosableInvocation.TargetMethod;

                if (diagnosableMethod.Equals(containsStringMethod) ||
                    diagnosableMethod.Equals(startsWithStringMethod) ||
                    diagnosableMethod.Equals(indexOfStringMethod) ||
                    diagnosableMethod.Equals(indexOfStringInt32Method) ||
                    diagnosableMethod.Equals(indexOfStringInt32Int32Method))
                {
                    context.ReportDiagnostic(diagnosableInvocation.CreateDiagnostic(RecommendCaseInsensitiveStringComparisonRule, diagnosableMethod.Name));
                }
                else if (diagnosableMethod.Equals(compareToStringMethod))
                {
                    context.ReportDiagnostic(diagnosableInvocation.CreateDiagnostic(RecommendCaseInsensitiveStringComparerRule));
                }
            }, OperationKind.Invocation);
        }
    }
}
