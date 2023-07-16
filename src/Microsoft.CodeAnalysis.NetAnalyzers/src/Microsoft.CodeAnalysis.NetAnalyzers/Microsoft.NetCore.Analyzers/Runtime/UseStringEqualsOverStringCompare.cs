// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2251: <inheritdoc cref="UseStringEqualsOverStringCompareTitle"/>
    /// Reports a diagnostic on any <see cref="IBinaryOperation"/> that:
    /// <list type="bullet">
    /// <item>Is an equals or not-equals operation</item>
    /// <item>One operand is a literal zero</item>
    /// <item>The other operand is an <see cref="IInvocationOperation"/> of an eligible
    /// <c>string.Compare</c> overload.</item>
    /// </list>
    /// See all the <c>Is...Case</c> methods to see the <c>string.Compare</c> overloads that are supported.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseStringEqualsOverStringCompare : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2251";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseStringEqualsOverStringCompareTitle)),
            CreateLocalizableResourceString(nameof(UseStringEqualsOverStringCompareMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeHidden_BulkConfigurable,
            CreateLocalizableResourceString(nameof(UseStringEqualsOverStringCompareDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
                return;
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Binary, OperationKind.Invocation);
            return;

            //  Local functions

            void AnalyzeOperation(OperationAnalysisContext context)
            {
                foreach (var selector in CaseSelectors)
                {
                    if (selector(context.Operation, symbols))
                    {
                        context.ReportDiagnostic(context.Operation.CreateDiagnostic(Rule));
                        return;
                    }
                }
            }
        }

        internal sealed class RequiredSymbols
        {
            private RequiredSymbols(
                INamedTypeSymbol stringType,
                INamedTypeSymbol boolType,
                INamedTypeSymbol stringComparisonType,
                IMethodSymbol? compareStringString,
                IMethodSymbol? compareStringStringBool,
                IMethodSymbol? compareStringStringStringComparison,
                IMethodSymbol? equalsStringString,
                IMethodSymbol? equalsStringStringStringComparison,
                IMethodSymbol intEquals)
            {
                StringType = stringType;
                BoolType = boolType;
                StringComparisonType = stringComparisonType;
                CompareStringString = compareStringString;
                CompareStringStringBool = compareStringStringBool;
                CompareStringStringStringComparison = compareStringStringStringComparison;
                EqualsStringString = equalsStringString;
                EqualsStringStringStringComparison = equalsStringStringStringComparison;
                IntEquals = intEquals;
            }

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);

                if (stringType is null || boolType is null)
                    return false;

                var typeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
                if (!typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison, out var stringComparisonType))
                    return false;

                var compareMethods = stringType.GetMembers(nameof(string.Compare))
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsStatic);
                var compareStringString = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType);
                var compareStringStringBool = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, boolType);
                var compareStringStringStringComparison = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, stringComparisonType);

                var equalsMethods = stringType.GetMembers(nameof(string.Equals))
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsStatic);
                var equalsStringString = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType);
                var equalsStringStringStringComparison = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, stringComparisonType);
                var intType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemInt32);
                var intEquals = intType
                    ?.GetMembers(nameof(int.Equals))
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.GetParameters() is [var param] && param.Type.Equals(intType, SymbolEqualityComparer.Default));
                if (intEquals is null)
                {
                    return false;
                }

                // Bail if we do not have at least one complete pair of Compare-Equals methods in the compilation.
                if ((compareStringString is null || equalsStringString is null) &&
                    (compareStringStringBool is null || equalsStringStringStringComparison is null) &&
                    (compareStringStringStringComparison is null || equalsStringStringStringComparison is null))
                {
                    return false;
                }

                symbols = new RequiredSymbols(
                    stringType, boolType, stringComparisonType,
                    compareStringString, compareStringStringBool, compareStringStringStringComparison,
                    equalsStringString, equalsStringStringStringComparison, intEquals);
                return true;
            }

            public INamedTypeSymbol StringType { get; }
            public INamedTypeSymbol BoolType { get; }
            public INamedTypeSymbol StringComparisonType { get; }
            public IMethodSymbol? CompareStringString { get; }
            public IMethodSymbol? CompareStringStringBool { get; }
            public IMethodSymbol? CompareStringStringStringComparison { get; }
            public IMethodSymbol? EqualsStringString { get; }
            public IMethodSymbol? EqualsStringStringStringComparison { get; }
            public IMethodSymbol IntEquals { get; }
        }

        /// <summary>
        /// If the specified <see cref="IBinaryOperation"/>:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is any <see cref="IInvocationOperation"/></item>
        /// </list>
        /// then this method returns the <see cref="IInvocationOperation"/>. 
        /// Otherwise, returns null.
        /// </summary>
        /// <param name="binaryOperation"></param>
        /// <returns></returns>
        internal static IInvocationOperation? GetInvocationFromEqualityCheckWithLiteralZero(IBinaryOperation? binaryOperation)
        {
            if (binaryOperation?.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals))
                return default;

            if (IsLiteralZero(binaryOperation.LeftOperand))
                return binaryOperation.RightOperand as IInvocationOperation;
            else if (IsLiteralZero(binaryOperation.RightOperand))
                return binaryOperation.LeftOperand as IInvocationOperation;
            else
                return default;

            //  Local functions

            static bool IsLiteralZero(IOperation? operation)
            {
                return operation is ILiteralOperation literal && literal.ConstantValue.Value is 0;
            }
        }

        internal static IInvocationOperation? GetInvocationFromEqualsCheckWithLiteralZero(IInvocationOperation? invocation, IMethodSymbol int32Equals)
        {
            if (!int32Equals.Equals(invocation?.TargetMethod.OriginalDefinition, SymbolEqualityComparer.Default))
            {
                return default;
            }

            if (invocation!.Arguments.FirstOrDefault()?.Value is ILiteralOperation { ConstantValue.Value: 0 })
            {
                return invocation.Instance as IInvocationOperation;
            }

            return default;
        }

        /// <summary>
        /// Returns true if the specified <see cref="IBinaryOperation"/>:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is any invocation of <see cref="string.Compare(string, string)"/></item>
        /// </list>
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        internal static bool IsStringStringCase(IOperation operation, RequiredSymbols symbols)
        {
            //  Don't report a diagnostic if either the string.Compare overload or the
            //  corresponding string.Equals overload is missing.
            if (symbols.CompareStringString is null ||
                symbols.EqualsStringString is null)
            {
                return false;
            }

            var invocation = GetInvocationFromEqualityCheckWithLiteralZero(operation as IBinaryOperation)
                ?? GetInvocationFromEqualsCheckWithLiteralZero(operation as IInvocationOperation, symbols.IntEquals);

            return invocation is not null &&
                invocation.TargetMethod.Equals(symbols.CompareStringString, SymbolEqualityComparer.Default);
        }

        /// <summary>
        /// Returns true if the specified <see cref="IBinaryOperation"/>:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is an invocation of <see cref="string.Compare(string, string, bool)"/></item>
        /// <item>The <c>ignoreCase</c> argument is a boolean literal</item>
        /// </list>
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        internal static bool IsStringStringBoolCase(IOperation operation, RequiredSymbols symbols)
        {
            //  Don't report a diagnostic if either the string.Compare overload or the
            //  corresponding string.Equals overload is missing.
            if (symbols.CompareStringStringBool is null ||
                symbols.EqualsStringStringStringComparison is null)
            {
                return false;
            }

            var invocation = GetInvocationFromEqualityCheckWithLiteralZero(operation as IBinaryOperation)
                ?? GetInvocationFromEqualsCheckWithLiteralZero(operation as IInvocationOperation, symbols.IntEquals);

            //  Only report a diagnostic if the 'ignoreCase' argument is a boolean literal.
            return invocation is not null &&
                invocation.TargetMethod.Equals(symbols.CompareStringStringBool, SymbolEqualityComparer.Default) &&
                invocation.Arguments.GetArgumentForParameterAtIndex(2).Value is ILiteralOperation literal &&
                literal.ConstantValue.Value is bool;
        }

        /// <summary>
        /// Returns true if the specified <see cref="IBinaryOperation"/>:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is any invocation of <see cref="string.Compare(string, string, StringComparison)"/></item>
        /// </list>
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        internal static bool IsStringStringStringComparisonCase(IOperation operation, RequiredSymbols symbols)
        {
            //  Don't report a diagnostic if either the string.Compare overload or the
            //  corrasponding string.Equals overload is missing.
            if (symbols.CompareStringStringStringComparison is null ||
                symbols.EqualsStringStringStringComparison is null)
            {
                return false;
            }

            var invocation = GetInvocationFromEqualityCheckWithLiteralZero(operation as IBinaryOperation)
                ?? GetInvocationFromEqualsCheckWithLiteralZero(operation as IInvocationOperation, symbols.IntEquals);

            return invocation is not null &&
                invocation.TargetMethod.Equals(symbols.CompareStringStringStringComparison, SymbolEqualityComparer.Default);
        }

        private static readonly ImmutableArray<Func<IOperation, RequiredSymbols, bool>> CaseSelectors =
            ImmutableArray.Create<Func<IOperation, RequiredSymbols, bool>>(
                IsStringStringCase,
                IsStringStringBoolCase,
                IsStringStringStringComparisonCase);
    }
}
