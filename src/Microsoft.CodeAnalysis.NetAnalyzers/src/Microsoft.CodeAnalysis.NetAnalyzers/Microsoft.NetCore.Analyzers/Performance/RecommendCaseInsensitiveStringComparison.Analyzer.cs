// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
        internal const string StringEqualsMethodName = "Equals";
        internal const string StringParameterName = "value";
        internal const string StringComparisonParameterName = "comparisonType";
        internal const string LeftOffendingMethodName = "LeftOffendingMethod";
        internal const string RightOffendingMethodName = "RightOffendingMethod";

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
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparisonTitle)),
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparerMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparerDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RecommendCaseInsensitiveStringEqualsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringComparisonTitle)),
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringEqualsMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(RecommendCaseInsensitiveStringEqualsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            RecommendCaseInsensitiveStringComparisonRule, RecommendCaseInsensitiveStringComparerRule, RecommendCaseInsensitiveStringEqualsRule);

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

            // Equals(string)
            IMethodSymbol? stringEqualsStringMethod = stringType.GetMembers(StringEqualsMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(stringParameter);
            if (stringEqualsStringMethod == null)
            {
                return;
            }

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

            ParameterInfo[] stringInt32Parameters = new[]
            {
                ParameterInfo.GetParameterInfo(stringType),
                ParameterInfo.GetParameterInfo(int32Type)
            };

            // IndexOf(string, int startIndex)
            IMethodSymbol? indexOfStringInt32Method = indexOfMethods.GetFirstOrDefaultMemberWithParameterInfos(stringInt32Parameters);
            if (indexOfStringInt32Method == null)
            {
                return;
            }

            ParameterInfo[] stringInt32Int32Parameters = new[]
            {
                ParameterInfo.GetParameterInfo(stringType),
                ParameterInfo.GetParameterInfo(int32Type),
                ParameterInfo.GetParameterInfo(int32Type)
            };

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

            ParameterInfo[] stringStringComparisonParameters = {
                ParameterInfo.GetParameterInfo(stringType),
                ParameterInfo.GetParameterInfo(stringComparisonType)
            };
            IMethodSymbol? containsStringWithStringComparisonMethod
                = stringType.GetMembers(StringContainsMethodName).OfType<IMethodSymbol>().GetFirstOrDefaultMemberWithParameterInfos(stringStringComparisonParameters);

            // a.ToLower().Method(b.ToLower())
            context.RegisterOperationAction(context =>
            {
                IInvocationOperation invocation = (IInvocationOperation)context.Operation;
                AnalyzeInvocation(context, invocation, stringType,
                    containsStringMethod, containsStringWithStringComparisonMethod, startsWithStringMethod, compareToStringMethod,
                    indexOfStringMethod, indexOfStringInt32Method, indexOfStringInt32Int32Method);
            }, OperationKind.Invocation);

            // a.ToLower() == b.ToLower()
            context.RegisterOperationAction(context =>
            {
                IBinaryOperation binaryOperation = (IBinaryOperation)context.Operation;
                AnalyzeBinaryOperation(context, binaryOperation, stringType);

            }, OperationKind.Binary);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context, IInvocationOperation invocation, INamedTypeSymbol stringType,
            IMethodSymbol containsStringMethod, IMethodSymbol? containsStringWithStringComparisonMethod, IMethodSymbol startsWithStringMethod, IMethodSymbol compareToStringMethod,
            IMethodSymbol indexOfStringMethod, IMethodSymbol indexOfStringInt32Method, IMethodSymbol indexOfStringInt32Int32Method)
        {
            IMethodSymbol diagnosableMethod = invocation.TargetMethod;

            DiagnosticDescriptor? chosenRule;
            if (diagnosableMethod.Equals(containsStringMethod) && containsStringWithStringComparisonMethod is not null ||
                diagnosableMethod.Equals(startsWithStringMethod) ||
                diagnosableMethod.Equals(indexOfStringMethod) ||
                diagnosableMethod.Equals(indexOfStringInt32Method) ||
                diagnosableMethod.Equals(indexOfStringInt32Int32Method))
            {
                chosenRule = RecommendCaseInsensitiveStringComparisonRule;
            }
            else if (diagnosableMethod.Equals(compareToStringMethod))
            {
                chosenRule = RecommendCaseInsensitiveStringComparerRule;
            }
            else
            {
                return;
            }

            bool atLeastOneOffendingInvocation = false;

            // First check if this is a case where the instance is a string that resulted from an offending
            // invocation, like {a.ToLower()}.Contains(), in which case we can collect the left side.
            string? leftOffendingMethodName = null;
            if (TryGetInvocationWithoutParentheses(invocation.Instance, out IInvocationOperation? maybeLeftOffendingInvocation))
            {
                atLeastOneOffendingInvocation = IsOffendingMethod(maybeLeftOffendingInvocation, stringType, out leftOffendingMethodName);
            }

            // Now check if the first argument of Contains|StartsWith|IndexOf is an invocation on a string
            // instance of one of the offending methods, in which case, we can collect the right side.
            Debug.Assert(!invocation.Arguments.IsEmpty);
            string? rightOffendingMethodName = null;
            if (TryGetInvocationWithoutParentheses(invocation.Arguments[0].Value, out IInvocationOperation? maybeRightOffendingInvocation))
            {
                atLeastOneOffendingInvocation |= IsOffendingMethod(maybeRightOffendingInvocation, stringType, out rightOffendingMethodName);
            }

            if (!atLeastOneOffendingInvocation)
            {
                // For a diagnosis on an invocation operation, either the instance of the invocation
                //  or the string instance of the first argument need to be an offending method.
                return;
            }

            ImmutableDictionary<string, string?> dict = new Dictionary<string, string?>()
            {
                { LeftOffendingMethodName, leftOffendingMethodName },
                { RightOffendingMethodName, rightOffendingMethodName }
            }.ToImmutableDictionary();

            context.ReportDiagnostic(invocation.CreateDiagnostic(chosenRule, dict, diagnosableMethod));
        }

        private static void AnalyzeBinaryOperation(OperationAnalysisContext context, IBinaryOperation binaryOperation, INamedTypeSymbol stringType)
        {
            if (binaryOperation.OperatorKind is not BinaryOperatorKind.Equals and not BinaryOperatorKind.NotEquals)
            {
                return;
            }

            bool atLeastOneOffendingInvocation = false;

            string? leftOffendingMethodName = null;
            if (TryGetInvocationWithoutParentheses(binaryOperation.LeftOperand, out IInvocationOperation? leftInvocation))
            {
                atLeastOneOffendingInvocation = IsOffendingMethod(leftInvocation, stringType, out leftOffendingMethodName);
            }

            string? rightOffendingMethodName = null;
            if (TryGetInvocationWithoutParentheses(binaryOperation.RightOperand, out IInvocationOperation? rightInvocation))
            {
                atLeastOneOffendingInvocation |= IsOffendingMethod(rightInvocation, stringType, out rightOffendingMethodName);
            }

            if (!atLeastOneOffendingInvocation)
            {
                // For a diagnosis on a binary operation, at least one of the two sides needs to
                // be an invocation of an offending method over a string instance.
                return;
            }

            ImmutableDictionary<string, string?> dict = new Dictionary<string, string?>()
                {
                    { LeftOffendingMethodName, leftOffendingMethodName },
                    { RightOffendingMethodName, rightOffendingMethodName }
                }.ToImmutableDictionary();

            context.ReportDiagnostic(binaryOperation.CreateDiagnostic(RecommendCaseInsensitiveStringEqualsRule, dict));
        }

        private static bool TryGetInvocationWithoutParentheses(IOperation? operation,
            [NotNullWhen(returnValue: true)] out IInvocationOperation? diagnosableInvocation)
        {
            diagnosableInvocation = null;

            IOperation? descendant = operation;
            while (descendant is IParenthesizedOperation parenthesizedOperation)
            {
                descendant = parenthesizedOperation.Operand;
            }

            if (descendant is IInvocationOperation invocationDescendant)
            {
                diagnosableInvocation = invocationDescendant;
            }
            else if (descendant is IArgumentOperation argumentDescendant && argumentDescendant.Value is IInvocationOperation argumentInvocationDescendant)
            {
                diagnosableInvocation = argumentInvocationDescendant;
            }

            return diagnosableInvocation != null;
        }

        private static bool IsOffendingMethod(IInvocationOperation invocation, ITypeSymbol stringType,
            [NotNullWhen(returnValue: true)] out string? offendingMethodName)
        {
            offendingMethodName = null;

            if (invocation.Instance == null || invocation.Instance.Type == null)
            {
                return false;
            }

            if (!invocation.Instance.Type.Equals(stringType))
            {
                return false;
            }

            if (!invocation.TargetMethod.Name.Equals(StringToLowerMethodName, StringComparison.Ordinal) &&
                !invocation.TargetMethod.Name.Equals(StringToLowerInvariantMethodName, StringComparison.Ordinal) &&
                !invocation.TargetMethod.Name.Equals(StringToUpperMethodName, StringComparison.Ordinal) &&
                !invocation.TargetMethod.Name.Equals(StringToUpperInvariantMethodName, StringComparison.Ordinal))
            {
                return false;
            }

            offendingMethodName = invocation.TargetMethod.Name;
            return true;
        }
    }
}
