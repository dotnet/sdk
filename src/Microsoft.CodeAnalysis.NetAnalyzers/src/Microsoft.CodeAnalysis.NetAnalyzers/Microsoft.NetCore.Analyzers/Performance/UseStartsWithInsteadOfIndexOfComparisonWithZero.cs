// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseStartsWithInsteadOfIndexOfComparisonWithZero : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1858";
        internal const string ShouldNegateKey = "ShouldNegate";
        internal const string CompilationHasStartsWithCharOverloadKey = "CompilationHasStartsWithCharOverload";

        internal const string ExistingOverloadKey = "ExistingOverload";

        internal const string OverloadString = "String";
        internal const string OverloadString_StringComparison = "String,StringComparison";
        internal const string OverloadChar = "Char";
        internal const string OverloadChar_StringComparison = "Char,StringComparison";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            id: RuleId,
            title: CreateLocalizableResourceString(nameof(UseStartsWithInsteadOfIndexOfComparisonWithZeroTitle)),
            messageFormat: CreateLocalizableResourceString(nameof(UseStartsWithInsteadOfIndexOfComparisonWithZeroMessage)),
            category: DiagnosticCategory.Performance,
            ruleLevel: RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(UseStartsWithInsteadOfIndexOfComparisonWithZeroDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false
            );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
                var hasAnyStartsWith = false;
                var hasStartsWithCharOverload = false;
                foreach (var startsWithMethod in stringType.GetMembers("StartsWith").OfType<IMethodSymbol>())
                {
                    hasAnyStartsWith = true;
                    if (startsWithMethod.Parameters is [{ Type.SpecialType: SpecialType.System_Char }])
                    {
                        hasStartsWithCharOverload = true;
                        break;
                    }
                }

                if (!hasAnyStartsWith)
                {
                    return;
                }

                var indexOfMethodsBuilder = ImmutableArray.CreateBuilder<(IMethodSymbol IndexOfSymbol, string OverloadPropertyValue)>();
                foreach (var indexOfMethod in stringType.GetMembers("IndexOf").OfType<IMethodSymbol>())
                {
                    if (indexOfMethod.Parameters is [{ Type.SpecialType: SpecialType.System_String }])
                    {
                        indexOfMethodsBuilder.Add((indexOfMethod, OverloadString));
                    }
                    else if (indexOfMethod.Parameters is [{ Type.SpecialType: SpecialType.System_Char }])
                    {
                        indexOfMethodsBuilder.Add((indexOfMethod, OverloadChar));
                    }
                    else if (indexOfMethod.Parameters is [{ Type.SpecialType: SpecialType.System_String }, { Name: "comparisonType" }])
                    {
                        indexOfMethodsBuilder.Add((indexOfMethod, OverloadString_StringComparison));
                    }
                    else if (indexOfMethod.Parameters is [{ Type.SpecialType: SpecialType.System_Char }, { Name: "comparisonType" }])
                    {
                        indexOfMethodsBuilder.Add((indexOfMethod, OverloadChar_StringComparison));
                    }
                }

                if (indexOfMethodsBuilder.Count == 0)
                {
                    return;
                }

                var indexOfMethods = indexOfMethodsBuilder.ToImmutable();

                context.RegisterOperationAction(context =>
                {
                    var binaryOperation = (IBinaryOperation)context.Operation;
                    if (binaryOperation.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals))
                    {
                        return;
                    }

                    if (IsIndexOfComparedWithZero(binaryOperation.LeftOperand, binaryOperation.RightOperand, indexOfMethods, out var additionalLocations, out var properties) ||
                        IsIndexOfComparedWithZero(binaryOperation.RightOperand, binaryOperation.LeftOperand, indexOfMethods, out additionalLocations, out properties))
                    {
                        if (binaryOperation.OperatorKind == BinaryOperatorKind.NotEquals)
                        {
                            properties = properties.Add(ShouldNegateKey, "");
                        }

                        if (hasStartsWithCharOverload)
                        {
                            properties = properties.Add(CompilationHasStartsWithCharOverloadKey, "");
                        }

                        context.ReportDiagnostic(binaryOperation.CreateDiagnostic(Rule, additionalLocations, properties));
                    }
                }, OperationKind.Binary);
            });
        }

        private static bool IsIndexOfComparedWithZero(
            IOperation left, IOperation right,
            ImmutableArray<(IMethodSymbol Symbol, string OverloadPropertyValue)> indexOfMethods,
            out ImmutableArray<Location> additionalLocations,
            out ImmutableDictionary<string, string?> properties)
        {
            properties = ImmutableDictionary<string, string?>.Empty;

            if (right.ConstantValue is { HasValue: true, Value: 0 } &&
                left is IInvocationOperation invocation)
            {
                foreach (var (indexOfMethod, overloadPropertyValue) in indexOfMethods)
                {
                    if (indexOfMethod.Equals(invocation.TargetMethod, SymbolEqualityComparer.Default))
                    {
                        var locationsBuilder = ImmutableArray.CreateBuilder<Location>();
                        locationsBuilder.Add(invocation.Instance!.Syntax.GetLocation());
                        locationsBuilder.AddRange(invocation.Arguments.Select(arg => arg.Syntax.GetLocation()));
                        additionalLocations = locationsBuilder.ToImmutable();

                        properties = properties.Add(ExistingOverloadKey, overloadPropertyValue);
                        return true;
                    }
                }
            }

            additionalLocations = ImmutableArray<Location>.Empty;
            return false;
        }
    }
}
