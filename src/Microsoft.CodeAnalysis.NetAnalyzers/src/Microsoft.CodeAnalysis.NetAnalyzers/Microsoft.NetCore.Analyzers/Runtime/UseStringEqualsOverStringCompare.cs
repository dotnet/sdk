// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseStringEqualsOverStringCompare : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2250";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(Resx.UseStringEqualsOverStringCompareTitle), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(Resx.UseStringEqualsOverStringCompareMessage), Resx.ResourceManager, typeof(Resx));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(Resx.UseStringEqualsOverStringCompareDescription), Resx.ResourceManager, typeof(Resx));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Usage,
            RuleLevel.IdeHidden_BulkConfigurable,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(context =>
            {
                if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
                    return;

                var selectors = GetSelectors(symbols);

                context.RegisterOperationAction(context =>
                {
                    foreach (var selector in selectors)
                    {
                        if (selector.IsMatch(context.Operation))
                        {
                            var diagnostic = context.Operation.CreateDiagnostic(Rule);
                            context.ReportDiagnostic(diagnostic);
                            break;
                        }
                    }
                }, OperationKind.Binary);
            });
        }

        internal static ImmutableArray<OperationSelector> GetSelectors(RequiredSymbols symbols)
        {
            return ImmutableArray.Create<OperationSelector>(
                new StringStringSelector(symbols),
                new StringStringBoolSelector(symbols),
                new StringStringStringComparisonSelector(symbols));
        }

        internal sealed class RequiredSymbols
        {
            //  Named-constructor 'TryGetSymbols' inits all properties or doesn't construct an instance.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            private RequiredSymbols() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);

                if (stringType is null || boolType is null)
                    return false;

                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison, out var stringComparisonType))
                    return false;

                var compareMethods = stringType.GetMembers(nameof(string.Compare))
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsStatic);
                var compareStringString = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType);
                var compareStringStringBool = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, boolType);
                var compareStringStringStringComparison = compareMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, stringComparisonType);

                if (compareStringString is null ||
                    compareStringStringBool is null ||
                    compareStringStringStringComparison is null)
                {
                    return false;
                }

                var equalsMethods = stringType.GetMembers(nameof(string.Equals))
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsStatic);
                var equalsStringString = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType);
                var equalsStringStringStringComparison = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, stringComparisonType);

                if (equalsStringString is null ||
                    equalsStringStringStringComparison is null)
                {
                    return false;
                }

                symbols = new RequiredSymbols
                {
                    StringType = stringType,
                    BoolType = boolType,
                    StringComparisonType = stringComparisonType,
                    CompareStringString = compareStringString,
                    CompareStringStringBool = compareStringStringBool,
                    CompareStringStringStringComparison = compareStringStringStringComparison,
                    EqualsStringString = equalsStringString,
                    EqualsStringStringStringComparison = equalsStringStringStringComparison
                };
                return true;
            }

            public INamedTypeSymbol StringType { get; init; }
            public INamedTypeSymbol BoolType { get; init; }
            public INamedTypeSymbol StringComparisonType { get; init; }
            public IMethodSymbol CompareStringString { get; init; }
            public IMethodSymbol CompareStringStringBool { get; init; }
            public IMethodSymbol CompareStringStringStringComparison { get; init; }
            public IMethodSymbol EqualsStringString { get; init; }
            public IMethodSymbol EqualsStringStringStringComparison { get; init; }
        }

        /// <summary>
        /// Selects an equals or not-equals operation where one argument is a literal zero, and the other argument
        /// is an eligible <c>string.Compare</c> invocation.
        /// </summary>
        internal abstract class OperationSelector
        {
            protected OperationSelector(RequiredSymbols symbols)
            {
                Symbols = symbols;
            }

            protected RequiredSymbols Symbols { get; }

            /// <summary>
            /// Indicates whether the specified <see cref="IOperation"/> matches the current <see cref="OperationSelector"/>
            /// </summary>
            /// <param name="compareResultToLiteralZero"></param>
            /// <returns></returns>
            public abstract bool IsMatch(IOperation compareResultToLiteralZero);

            /// <summary>
            /// Creates a replacement expression for the specified matching <see cref="IOperation"/>. Asserts if
            /// <see cref="IsMatch(IOperation)"/> returns <see langword="false" /> for the specified <see cref="IOperation"/>.
            /// </summary>
            /// <param name="compareResultToLiteralZero">An <see cref="IOperation"/> that is matched by the current <see cref="OperationSelector"/></param>
            /// <param name="generator">The <see cref="SyntaxGenerator"/> to use.</param>
            /// <returns>The replacement expression to be used by the code fixer.</returns>
            public abstract SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator);

            /// <summary>
            /// Tries to get an invocation operation that is being compared to a literal zero.
            /// </summary>
            /// <param name="compareResultToLiteralZero">An operation that is potentially a comparison of an invocation with a literal zero.</param>
            /// <param name="invocation">The invocation operation.</param>
            /// <returns>True if the specified operation is an equals or not-equals operation that compares a literal zero to 
            /// any invocation operation.</returns>
            protected static bool TryGetInvocationFromComparisonWithLiteralZero(IOperation compareResultToLiteralZero, [NotNullWhen(true)] out IInvocationOperation? invocation)
            {
                if (compareResultToLiteralZero is IBinaryOperation binaryOperation &&
                    binaryOperation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
                {
                    if (TryConvertOperands(binaryOperation.LeftOperand, binaryOperation.RightOperand, out invocation) ||
                        TryConvertOperands(binaryOperation.RightOperand, binaryOperation.LeftOperand, out invocation))
                    {
                        return true;
                    }
                }

                invocation = default;
                return false;

                //  Local functions

                static bool TryConvertOperands(IOperation first, IOperation second, [NotNullWhen(true)] out IInvocationOperation? result)
                {
                    if (first is IInvocationOperation invocation &&
                        second is ILiteralOperation literal &&
                        literal.ConstantValue.HasValue &&
                        literal.ConstantValue.Value is int integer &&
                        integer is 0)
                    {
                        result = invocation;
                        return true;
                    }

                    result = default;
                    return false;
                }
            }

            protected static IInvocationOperation GetInvocationFromComparisonWithLiteralZero(IOperation compareResultToLiteralZero)
            {
                if (!TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation))
                    Fail();

                return invocation;
            }

            protected static bool IsNotEqualsOperation(IOperation operation)
            {
                return operation is IBinaryOperation binaryOperation &&
                    binaryOperation.OperatorKind is BinaryOperatorKind.NotEquals;
            }

            [DoesNotReturn]
#pragma warning disable CS8763 // A method marked [DoesNotReturn] should not return.
            protected static void Fail() => Debug.Fail($"'{nameof(GetReplacementExpression)}' must only be called when '{nameof(IsMatch)}' is 'true'.");
#pragma warning restore CS8763 // A method marked [DoesNotReturn] should not return.

            protected SyntaxNode CreateStringEqualsMemberAccessExpression(SyntaxGenerator generator)
            {
                var stringTypeExpression = generator.TypeExpressionForStaticMemberAccess(Symbols.StringType);
                return generator.MemberAccessExpression(stringTypeExpression, nameof(string.Equals));
            }
        }

        /// <summary>
        /// Selects <see cref="IOperation"/>s that satisfy all of the following:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is an invocation of <see cref="string.Compare(string, string)"/></item>
        /// </list>
        /// </summary>
        private sealed class StringStringSelector : OperationSelector
        {
            public StringStringSelector(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IOperation compareResultToLiteralZero)
            {
                return TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation) &&
                    invocation.TargetMethod.Equals(Symbols.CompareStringString, SymbolEqualityComparer.Default);
            }

            public override SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(compareResultToLiteralZero));

                var invocation = GetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero);
                var equalsMemberAccessExpression = CreateStringEqualsMemberAccessExpression(generator);
                var equalsInvocationExpression = generator.InvocationExpression(
                    equalsMemberAccessExpression,
                    invocation.Arguments.GetArgumentsInParameterOrder().Select(x => x.Value.Syntax));

                return IsNotEqualsOperation(compareResultToLiteralZero) ?
                    generator.LogicalNotExpression(equalsInvocationExpression) :
                    equalsInvocationExpression;
            }
        }

        /// <summary>
        /// Selects <see cref="IOperation"/>s that satisfy all of the following:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is an invocation of <see cref="string.Compare(string, string, bool)"/></item>
        /// <item>The <see langword="bool"/> argument is a literal</item>
        /// </list>
        /// </summary>
        private sealed class StringStringBoolSelector : OperationSelector
        {
            public StringStringBoolSelector(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IOperation compareResultToLiteralZero)
            {
                //  The 'ignoreCase' bool argument of string.Compare must be a literal.
                return TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation) &&
                    invocation.TargetMethod.Equals(Symbols.CompareStringStringBool, SymbolEqualityComparer.Default) &&
                    invocation.Arguments.GetArgumentForParameterAtIndex(2).Value is ILiteralOperation boolLiteral &&
                    boolLiteral.ConstantValue.HasValue &&
                    boolLiteral.ConstantValue.Value is bool;
            }

            public override SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(compareResultToLiteralZero));

                var invocation = GetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero);

                //  'IsMatch' rejects operations where the 'ignoreCase' argument is not a literal.
                var ignoreCaseLiteral = (ILiteralOperation)invocation.Arguments.GetArgumentForParameterAtIndex(2).Value;

                var equalsMemberAccessExpression = CreateStringEqualsMemberAccessExpression(generator);
                var stringComparisonTypeExpression = generator.TypeExpressionForStaticMemberAccess(Symbols.StringComparisonType);

                //  Convert 'ignoreCase' boolean argument to equivalent StringComparison value.
                var stringComparisonEnumMemberName = (bool)ignoreCaseLiteral.ConstantValue.Value ?
                    nameof(StringComparison.CurrentCultureIgnoreCase) :
                    nameof(StringComparison.CurrentCulture);
                var stringComparisonEnumMemberAccessExpression = generator.MemberAccessExpression(stringComparisonTypeExpression, stringComparisonEnumMemberName);

                var equalsInvocationExpression = generator.InvocationExpression(
                    equalsMemberAccessExpression,
                    invocation.Arguments.GetArgumentForParameterAtIndex(0).Value.Syntax,
                    invocation.Arguments.GetArgumentForParameterAtIndex(1).Value.Syntax,
                    stringComparisonEnumMemberAccessExpression);

                return IsNotEqualsOperation(compareResultToLiteralZero) ?
                    generator.LogicalNotExpression(equalsInvocationExpression) :
                    equalsInvocationExpression;
            }
        }

        /// <summary>
        /// Selects <see cref="IOperation"/>s that satisfy all of the following:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is an invocation of <see cref="string.Compare(string, string, StringComparison)"/></item>
        /// </list>
        /// </summary>
        private sealed class StringStringStringComparisonSelector : OperationSelector
        {
            public StringStringStringComparisonSelector(RequiredSymbols symbols)
                : base(symbols)
            { }

            public override bool IsMatch(IOperation compareResultToLiteralZero)
            {
                return TryGetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero, out var invocation) &&
                    invocation.TargetMethod.Equals(Symbols.CompareStringStringStringComparison, SymbolEqualityComparer.Default);
            }

            public override SyntaxNode GetReplacementExpression(IOperation compareResultToLiteralZero, SyntaxGenerator generator)
            {
                RoslynDebug.Assert(IsMatch(compareResultToLiteralZero));

                var invocation = GetInvocationFromComparisonWithLiteralZero(compareResultToLiteralZero);

                var equalsMemberAccessExpression = CreateStringEqualsMemberAccessExpression(generator);

                var equalsInvocationExpression = generator.InvocationExpression(
                    equalsMemberAccessExpression,
                    invocation.Arguments.GetArgumentsInParameterOrder().Select(x => x.Value.Syntax));

                return IsNotEqualsOperation(compareResultToLiteralZero) ?
                    generator.LogicalNotExpression(equalsInvocationExpression) :
                    equalsInvocationExpression;
            }
        }
    }
}
