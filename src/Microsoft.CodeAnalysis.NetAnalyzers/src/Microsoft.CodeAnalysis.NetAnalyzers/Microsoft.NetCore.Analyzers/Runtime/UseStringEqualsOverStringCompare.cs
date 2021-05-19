// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
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
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
                return;
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Binary);
            return;

            //  Local functions

            void AnalyzeOperation(OperationAnalysisContext context)
            {
                var operation = (IBinaryOperation)context.Operation;
                foreach (var selector in CaseSelectors)
                {
                    if (selector(operation, symbols))
                    {
                        context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
                        return;
                    }
                }
            }
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

                var equalsMethods = stringType.GetMembers(nameof(string.Equals))
                    .OfType<IMethodSymbol>()
                    .Where(x => x.IsStatic);
                var equalsStringString = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType);
                var equalsStringStringStringComparison = equalsMethods.GetFirstOrDefaultMemberWithParameterTypes(stringType, stringType, stringComparisonType);

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
            public IMethodSymbol? CompareStringString { get; init; }
            public IMethodSymbol? CompareStringStringBool { get; init; }
            public IMethodSymbol? CompareStringStringStringComparison { get; init; }
            public IMethodSymbol? EqualsStringString { get; init; }
            public IMethodSymbol? EqualsStringStringStringComparison { get; init; }
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
        internal static IInvocationOperation? GetInvocationFromEqualityCheckWithLiteralZero(IBinaryOperation binaryOperation)
        {
            if (binaryOperation.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals))
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

        /// <summary>
        /// Returns true if the specified <see cref="IBinaryOperation"/>:
        /// <list type="bullet">
        /// <item>Is an equals or not-equals operation</item>
        /// <item>One operand is a literal zero</item>
        /// <item>The other operand is any invocation of <see cref="string.Compare(string, string)"/></item>
        /// </list>
        /// </summary>
        /// <param name="binaryOperation"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        internal static bool IsStringStringCase(IBinaryOperation binaryOperation, RequiredSymbols symbols)
        {
            var invocation = GetInvocationFromEqualityCheckWithLiteralZero(binaryOperation);

            return invocation is not null &&
                //  Don't report a diagnostic if either the string.Compare overload or the
                //  corrasponding string.Equals overload is missing.
                symbols.CompareStringString is not null &&
                symbols.EqualsStringString is not null &&
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
        /// <param name="binaryOperation"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        internal static bool IsStringStringBoolCase(IBinaryOperation binaryOperation, RequiredSymbols symbols)
        {
            var invocation = GetInvocationFromEqualityCheckWithLiteralZero(binaryOperation);

            return invocation is not null &&
                //  Don't report a diagnostic if either the string.Compare overload or the
                //  corrasponding string.Equals overload is missing.
                symbols.CompareStringStringBool is not null &&
                symbols.EqualsStringStringStringComparison is not null &&
                invocation.TargetMethod.Equals(symbols.CompareStringStringBool) &&
                //  Only report a diagnostic if the 'ignoreCase' argument is a boolean literal.
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
        /// <param name="binaryOperation"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
        internal static bool IsStringStringStringComparisonCase(IBinaryOperation binaryOperation, RequiredSymbols symbols)
        {
            var invocation = GetInvocationFromEqualityCheckWithLiteralZero(binaryOperation);

            return invocation is not null &&
                //  Don't report a diagnostic if either the string.Compare overload or the
                //  corrasponding string.Equals overload is missing.
                symbols.CompareStringStringStringComparison is not null &&
                symbols.EqualsStringStringStringComparison is not null &&
                invocation.TargetMethod.Equals(symbols.CompareStringStringStringComparison, SymbolEqualityComparer.Default);
        }

        //  No IOperation instances are being stored here.
#pragma warning disable RS1008 // Avoid storing per-compilation data into the fields of a diagnostic analyzer
        private static readonly ImmutableArray<Func<IBinaryOperation, RequiredSymbols, bool>> CaseSelectors =
#pragma warning restore RS1008 // Avoid storing per-compilation data into the fields of a diagnostic analyzer
            ImmutableArray.Create<Func<IBinaryOperation, RequiredSymbols, bool>>(
                IsStringStringCase,
                IsStringStringBoolCase,
                IsStringStringStringComparisonCase);
    }
}
