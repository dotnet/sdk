// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class UseSpanBasedStringConcat : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1841";

        private static readonly LocalizableString s_localizableTitle = CreateResource(nameof(Resx.UseSpanBasedStringConcatTitle));
        private static readonly LocalizableString s_localizableMessage = CreateResource(nameof(Resx.UseSpanBasedStringConcatMessage));
        private static readonly LocalizableString s_localizableDescription = CreateResource(nameof(Resx.UseSpanBasedStringConcatDescription));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        private const string SubstringName = nameof(string.Substring);
        private const string AsSpanName = nameof(MemoryExtensions.AsSpan);
        private const string ConcatName = nameof(string.Concat);

        /// <summary>
        /// If the specified binary operation is a string concatenation operation, we try to walk up to the top-most
        /// string-concatenation operation that it is part of. If it is not a string-concatenation operation, we simply
        /// return false.
        /// </summary>
        private protected abstract bool TryGetTopMostConcatOperation(IBinaryOperation binaryOperation, [NotNullWhen(true)] out IBinaryOperation? rootBinaryOperation);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out RequiredSymbols symbols))
                return;

            context.RegisterOperationBlockStartAction(OnOperationBlockStart);

            void OnOperationBlockStart(OperationBlockStartAnalysisContext context)
            {
                //  Maintain set of all top-most concat operations so we don't report sub-expressions of an
                //  already-reported violation. 
                //  We also don't report any diagnostic if the concat operation has too many operands for the span-based
                //  Concat overloads to handle.
                var topMostConcatOperations = PooledConcurrentSet<IBinaryOperation>.GetInstance();

                context.RegisterOperationAction(PopulateTopMostConcatOperations, OperationKind.Binary);
                context.RegisterOperationBlockEndAction(ReportDiagnosticsOnRootConcatOperationsWithSubstringCalls);

                void PopulateTopMostConcatOperations(OperationAnalysisContext context)
                {
                    //  If the current operation is a string-concatenation operation, walk up to the top-most concat
                    //  operation and add it to the set.
                    var binary = (IBinaryOperation)context.Operation;
                    if (!TryGetTopMostConcatOperation(binary, out var topMostConcatOperation))
                        return;

                    topMostConcatOperations.Add(topMostConcatOperation);
                }

                void ReportDiagnosticsOnRootConcatOperationsWithSubstringCalls(OperationBlockAnalysisContext context)
                {
                    //  We report diagnostics for all top-most concat operations that contain 
                    //  direct or conditional substring invocations
                    //  when there is an applicable span-based overload of string.Concat
                    foreach (var operation in topMostConcatOperations)
                    {
                        var chain = FlattenBinaryOperation(operation);
                        if (chain.Any(IsAnyDirectOrConditionalSubstringInvocation) && symbols.TryGetRoscharConcatMethodWithArity(chain.Length, out _))
                        {
                            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
                        }
                    }
                    topMostConcatOperations.Free(context.CancellationToken);
                }
            }

            bool IsAnyDirectOrConditionalSubstringInvocation(IOperation operation)
            {
                var value = WalkDownBuiltInImplicitConversionOnConcatOperand(operation);
                if (value is IConditionalAccessOperation conditionallAccessOperation)
                    value = conditionallAccessOperation.WhenNotNull;

                return value is IInvocationOperation invocation && symbols.IsAnySubstringMethod(invocation.TargetMethod);
            }
        }

        internal static ImmutableArray<IOperation> FlattenBinaryOperation(IBinaryOperation root)
        {
            var stack = new Stack<IBinaryOperation>();
            var builder = ImmutableArray.CreateBuilder<IOperation>();
            GoLeft(root);

            while (stack.Count != 0)
            {
                var current = stack.Pop();

                if (current.LeftOperand is not IBinaryOperation leftBinary || leftBinary.OperatorKind != root.OperatorKind)
                {
                    builder.Add(current.LeftOperand);
                }

                if (current.RightOperand is not IBinaryOperation rightBinary || rightBinary.OperatorKind != root.OperatorKind)
                {
                    builder.Add(current.RightOperand);
                }
                else
                {
                    GoLeft(rightBinary);
                }
            }

            return builder.ToImmutable();

            void GoLeft(IBinaryOperation operation)
            {
                IBinaryOperation? current = operation;
                while (current is not null && current.OperatorKind == root.OperatorKind)
                {
                    stack.Push(current);
                    current = current.LeftOperand as IBinaryOperation;
                }
            }
        }

        /// <summary>
        /// Remove the built in implicit conversion on operands to concat.
        /// In VB, the conversion can be to either string or object.
        /// In C#, the conversion is always to object.
        /// </summary>
        internal static IOperation WalkDownBuiltInImplicitConversionOnConcatOperand(IOperation operand)
        {
            if (operand is not IConversionOperation conversion)
                return operand;
            if (!conversion.IsImplicit || conversion.Conversion.IsUserDefined)
                return conversion;

            switch (conversion.Language)
            {
                case LanguageNames.CSharp when conversion.Type.SpecialType is SpecialType.System_Object:
                case LanguageNames.VisualBasic when conversion.Type.SpecialType is SpecialType.System_Object or SpecialType.System_String:
                    return conversion.Operand;
                default:
                    return conversion;
            }
        }

        // Use readonly struct instead of record type to save on allocations, since it's not passed by-value.
        // We aren't comparing these.
#pragma warning disable CA1815 // Override equals and operator equals on value types
        internal readonly struct RequiredSymbols
#pragma warning restore CA1815 // Override equals and operator equals on value types
        {
            private RequiredSymbols(
                INamedTypeSymbol stringType, INamedTypeSymbol roscharType,
                IMethodSymbol substring1, IMethodSymbol substring2,
                IMethodSymbol asSpan1, IMethodSymbol asSpan2)
            {
                StringType = stringType;
                RoscharType = roscharType;
                Substring1 = substring1;
                Substring2 = substring2;
                AsSpan1 = asSpan1;
                AsSpan2 = asSpan2;

                RoslynDebug.Assert(
                    StringType is not null && RoscharType is not null &&
                    Substring1 is not null && Substring2 is not null &&
                    AsSpan1 is not null && AsSpan2 is not null);
            }

            public static bool TryGetSymbols(Compilation compilation, out RequiredSymbols symbols)
            {
                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var charType = compilation.GetSpecialType(SpecialType.System_Char);

                if (stringType is null || charType is null)
                {
                    symbols = default;
                    return false;
                }

                var roscharType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1)?.Construct(charType);
                var memoryExtensionsType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions);

                if (roscharType is null || memoryExtensionsType is null)
                {
                    symbols = default;
                    return false;
                }

                var intParamInfo = ParameterInfo.GetParameterInfo(compilation.GetSpecialType(SpecialType.System_Int32));
                var stringParamInfo = ParameterInfo.GetParameterInfo(stringType);

                var substringMembers = stringType.GetMembers(SubstringName).OfType<IMethodSymbol>();
                var substring1 = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(intParamInfo);
                var substring2 = substringMembers.GetFirstOrDefaultMemberWithParameterInfos(intParamInfo, intParamInfo);

                var asSpanMembers = memoryExtensionsType.GetMembers(AsSpanName).OfType<IMethodSymbol>();
                var asSpan1 = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, intParamInfo)?.ReduceExtensionMethod(stringType);
                var asSpan2 = asSpanMembers.GetFirstOrDefaultMemberWithParameterInfos(stringParamInfo, intParamInfo, intParamInfo)?.ReduceExtensionMethod(stringType);

                if (substring1 is null || substring2 is null || asSpan1 is null || asSpan2 is null)
                {
                    symbols = default;
                    return false;
                }

                symbols = new RequiredSymbols(
                    stringType, roscharType,
                    substring1, substring2,
                    asSpan1, asSpan2);
                return true;
            }

            public INamedTypeSymbol StringType { get; }
            public INamedTypeSymbol RoscharType { get; }
            public IMethodSymbol Substring1 { get; }
            public IMethodSymbol Substring2 { get; }
            public IMethodSymbol AsSpan1 { get; }
            public IMethodSymbol AsSpan2 { get; }

            public IMethodSymbol? GetAsSpanEquivalent(IMethodSymbol? substringMethod)
            {
                if (SymbolEqualityComparer.Default.Equals(substringMethod, Substring1))
                    return AsSpan1;
                if (SymbolEqualityComparer.Default.Equals(substringMethod, Substring2))
                    return AsSpan2;
                return null;
            }

            public bool IsAnySubstringMethod(IMethodSymbol? method)
            {
                return SymbolEqualityComparer.Default.Equals(method, Substring1) ||
                    SymbolEqualityComparer.Default.Equals(method, Substring2);
            }

            public bool IsAnySubstringStartIndexParameter(IParameterSymbol? parameter)
            {
                return SymbolEqualityComparer.Default.Equals(parameter, Substring1.Parameters.First()) ||
                    SymbolEqualityComparer.Default.Equals(parameter, Substring2.Parameters.First());
            }

            public bool TryGetRoscharConcatMethodWithArity(int arity, [NotNullWhen(true)] out IMethodSymbol? concatMethod)
            {
                var roscharParamInfo = ParameterInfo.GetParameterInfo(RoscharType);
                var argumentList = new ParameterInfo[arity];
                for (int index = 0; index < arity; index++)
                    argumentList[index] = roscharParamInfo;

                concatMethod = StringType.GetMembers(ConcatName)
                    .OfType<IMethodSymbol>()
                    .GetFirstOrDefaultMemberWithParameterInfos(argumentList);
                return concatMethod is not null;
            }
        }

        private static LocalizableString CreateResource(string resourceName)
        {
            return new LocalizableResourceString(resourceName, Resx.ResourceManager, typeof(Resx));
        }
    }
}
