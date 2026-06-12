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

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1872: <inheritdoc cref="PreferConvertToHexStringOverBitConverterTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferConvertToHexStringOverBitConverterAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1872";
        internal const string ReplacementPropertiesKey = nameof(ReplacementPropertiesKey);

        private const string Empty = nameof(Empty);
        private const string Replace = nameof(Replace);
        private const string ToHexString = nameof(ToHexString);
        private const string ToHexStringLower = nameof(ToHexStringLower);
        private const string ToLower = nameof(ToLower);
        private const string ToLowerInvariant = nameof(ToLowerInvariant);

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferConvertToHexStringOverBitConverterTitle)),
            CreateLocalizableResourceString(nameof(PreferConvertToHexStringOverBitConverterMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(PreferConvertToHexStringOverBitConverterDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (!RequiredSymbols.TryGetSymbols(context.Compilation, out var symbols))
            {
                return;
            }

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

            void AnalyzeInvocation(OperationAnalysisContext context)
            {
                var invocation = (IInvocationOperation)context.Operation;

                if (!symbols.TryGetBitConverterInvocationChain(
                        invocation,
                        out var bitConverterInvocation,
                        out var outerInvocation,
                        out var toLowerInvocation,
                        out var convertToHexStringReplacementMethod))
                {
                    return;
                }

                var additionalLocationsBuilder = ImmutableArray.CreateBuilder<Location>();
                additionalLocationsBuilder.Add(bitConverterInvocation.Syntax.GetLocation());

                if (toLowerInvocation is not null)
                {
                    additionalLocationsBuilder.Add(toLowerInvocation.Syntax.GetLocation());
                }

                context.ReportDiagnostic(outerInvocation.CreateDiagnostic(
                    Rule,
                    additionalLocations: additionalLocationsBuilder.ToImmutable(),
                    properties: ImmutableDictionary<string, string?>.Empty.Add(ReplacementPropertiesKey, convertToHexStringReplacementMethod.Name),
                    args:[convertToHexStringReplacementMethod.ToDisplayString(), bitConverterInvocation.TargetMethod.ToDisplayString()]));
            }
        }

        private sealed class RequiredSymbols
        {
            private RequiredSymbols(
                ImmutableArray<IMethodSymbol> stringReplaceMethods,
                ImmutableArray<IMethodSymbol> stringToLowerMethods,
                IFieldSymbol? stringEmptyField,
                ImmutableDictionary<IMethodSymbol, IMethodSymbol> bitConverterReplacements,
                ImmutableDictionary<IMethodSymbol, IMethodSymbol> bitConverterReplacementsToLower)
            {
                _stringReplaceMethods = stringReplaceMethods;
                _stringToLowerMethods = stringToLowerMethods;
                _stringEmptyField = stringEmptyField;
                _bitConverterReplacements = bitConverterReplacements;
                _bitConverterReplacementsToLower = bitConverterReplacementsToLower;
            }

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var bitConverterType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemBitConverter);
                var convertType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConvert);

                if (bitConverterType is null || convertType is null)
                {
                    return false;
                }

                var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
                var byteArrayType = compilation.CreateArrayTypeSymbol(byteType);
                var int32Type = compilation.GetSpecialType(SpecialType.System_Int32);
                var stringType = compilation.GetSpecialType(SpecialType.System_String);
                var rosByteType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1)?.Construct(byteType);

                var stringReplaceMethods = stringType.GetMembers(Replace)
                    .OfType<IMethodSymbol>()
                    .WhereAsArray(FilterStringReplaceMethods);

                if (stringReplaceMethods.IsEmpty)
                {
                    return false;
                }

                var stringToLowerMethods = stringType.GetMembers(ToLower)
                    .AddRange(stringType.GetMembers(ToLowerInvariant))
                    .OfType<IMethodSymbol>()
                    .ToImmutableArray();
                var stringEmptyField = stringType.GetMembers(Empty)
                    .OfType<IFieldSymbol>()
                    .FirstOrDefault();

                var bitConverterToStringMethods = bitConverterType.GetMembers(WellKnownMemberNames.ObjectToString)
                    .OfType<IMethodSymbol>()
                    .ToImmutableArray();

                var bitConverterToString = bitConverterToStringMethods.GetFirstOrDefaultMemberWithParameterTypes(byteArrayType);
                var bitConverterToStringStart = bitConverterToStringMethods.GetFirstOrDefaultMemberWithParameterTypes(byteArrayType, int32Type);
                var bitConverterToStringStartLength = bitConverterToStringMethods.GetFirstOrDefaultMemberWithParameterTypes(byteArrayType, int32Type, int32Type);

                var convertToHexStringMethods = convertType.GetMembers(ToHexString)
                    .OfType<IMethodSymbol>()
                    .ToImmutableArray();

                var convertToHexString = convertToHexStringMethods.GetFirstOrDefaultMemberWithParameterTypes(byteArrayType);
                var convertToHexStringRos = rosByteType is not null ? convertToHexStringMethods.GetFirstOrDefaultMemberWithParameterTypes(rosByteType) : null;
                var convertToHexStringStartLength = convertToHexStringMethods.GetFirstOrDefaultMemberWithParameterTypes(byteArrayType, int32Type, int32Type);

                var bitConverterReplacementsBuilder = ImmutableDictionary.CreateBuilder<IMethodSymbol, IMethodSymbol>();
                // BitConverter.ToString(data).Replace("-", "") => Convert.ToHexString(data)
                bitConverterReplacementsBuilder.AddKeyValueIfNotNull(bitConverterToString, convertToHexString);
                // BitConverter.ToString(data, start).Replace("-", "") => Convert.ToHexString(data.AsSpan().Slice(start))
                bitConverterReplacementsBuilder.AddKeyValueIfNotNull(bitConverterToStringStart, convertToHexStringRos);
                // BitConverter.ToString(data, start, length).Replace("-", "") => Convert.ToHexString(data, start, length)
                bitConverterReplacementsBuilder.AddKeyValueIfNotNull(bitConverterToStringStartLength, convertToHexStringStartLength);
                var bitConverterReplacements = bitConverterReplacementsBuilder.ToImmutableDictionary();

                // Bail out if we have no valid replacement pair from BitConverter.ToString to Convert.ToHexString.
                if (bitConverterReplacements.IsEmpty)
                {
                    return false;
                }

                var convertToHexStringLowerMethods = convertType.GetMembers(ToHexStringLower)
                    .OfType<IMethodSymbol>()
                    .ToImmutableArray();

                var convertToHexStringLower = convertToHexStringLowerMethods.GetFirstOrDefaultMemberWithParameterTypes(byteArrayType);
                var convertToHexStringLowerRos = rosByteType is not null ? convertToHexStringLowerMethods.GetFirstOrDefaultMemberWithParameterTypes(rosByteType) : null;
                var convertToHexStringLowerStartLength = convertToHexStringLowerMethods.GetFirstOrDefaultMemberWithParameterTypes(byteArrayType, int32Type, int32Type);

                // The following replacements are optional: Convert.ToHexStringLower is available as of .NET 9.
                var bitConverterReplacementsToLowerBuilder = ImmutableDictionary.CreateBuilder<IMethodSymbol, IMethodSymbol>();
                // BitConverter.ToString(data).Replace("-", "").ToLower() => Convert.ToHexStringLower(data)
                bitConverterReplacementsToLowerBuilder.AddKeyValueIfNotNull(bitConverterToString, convertToHexStringLower);
                // BitConverter.ToString(data, start).Replace("-", "").ToLower() => Convert.ToHexStringLower(data.AsSpan().Slice(start))
                bitConverterReplacementsToLowerBuilder.AddKeyValueIfNotNull(bitConverterToStringStart, convertToHexStringLowerRos);
                // BitConverter.ToString(data, start, length).Replace("-", "").ToLower() => Convert.ToHexStringLower(data, start, length)
                bitConverterReplacementsToLowerBuilder.AddKeyValueIfNotNull(bitConverterToStringStartLength, convertToHexStringLowerStartLength);
                var bitConverterReplacementsToLower = bitConverterReplacementsToLowerBuilder.ToImmutableDictionary();

                symbols = new RequiredSymbols(stringReplaceMethods, stringToLowerMethods,
                    stringEmptyField, bitConverterReplacements, bitConverterReplacementsToLower);

                return true;

                bool FilterStringReplaceMethods(IMethodSymbol stringReplaceMethod)
                {
                    return stringReplaceMethod.Parameters.Length >= 2 &&
                        SymbolEqualityComparer.Default.Equals(stringReplaceMethod.Parameters[0].Type, stringType) &&
                        SymbolEqualityComparer.Default.Equals(stringReplaceMethod.Parameters[1].Type, stringType);
                }
            }

            /// <summary>
            /// Attempts to obtain a complete BitConverter.ToString invocation chain that can be replaced with a call to Convert.ToHexString*.
            /// To do this, the following steps are performed:
            ///   1. Check if invocation is a BitConverter.ToString invocation and abort if it is not.
            ///   2. Continue with the parent invocation or abort if none exists.
            ///   3. Check if invocation is a string.ToLower* invocation.
            ///      If so, continue with the parent invocation or abort if none exists.
            ///   4. Check if invocation is a string.Replace("-", "") invocation and abort if it is not.
            ///      If so, continue with the parent invocation or skip the next step if none exists.
            ///   5. Check if invocation is a string.ToLower* invocation.
            ///   6. Get the appropriate replacement method (from BitConverter.ToString to Convert.ToHexString or Convert.ToHexStringLower) or abort if none exists.
            /// Note that <paramref name="toLowerInvocation"/> is only set if a string.ToLower* invocation is found and Convert.ToHexStringLower is not available.
            /// </summary>
            /// <param name="invocation">The starting invocation to analyze (must be BitConverter.ToString for this method to return true)</param>
            /// <param name="bitConverterInvocation">The extracted BitConverter.ToString invocation, or null if unsuccessful</param>
            /// <param name="outerInvocation">The outer invocation that is used for creating the diagnostic (and that will be replaced in the fixer)</param>
            /// <param name="toLowerInvocation">The extracted string.ToLower* invocation, or null if none exists or Convert.ToHexStringLower is used as a replacement</param>
            /// <param name="replacementMethod">The Convert.ToHexString* method to use as a replacement</param>
            /// <returns></returns>
            public bool TryGetBitConverterInvocationChain(
                IInvocationOperation invocation,
                [NotNullWhen(true)] out IInvocationOperation? bitConverterInvocation,
                [NotNullWhen(true)] out IInvocationOperation? outerInvocation,
                out IInvocationOperation? toLowerInvocation,
                [NotNullWhen(true)] out IMethodSymbol? replacementMethod)
            {
                bitConverterInvocation = default;
                outerInvocation = default;
                toLowerInvocation = default;
                replacementMethod = default;

                // Bail out if the invocation is not a BitConverter.ToString invocation.
                if (!IsAnyBitConverterMethod(invocation.TargetMethod))
                {
                    return false;
                }

                bitConverterInvocation = invocation;

                // Bail out if there is no parent invocation.
                if (!TryGetParentInvocation(invocation, out invocation!))
                {
                    return false;
                }

                // Check for an optional string.ToLower invocation, e.g. in the case of
                // BitConverter.ToString(data).ToLower().Replace("-", "")
                if (IsAnyStringToLowerMethod(invocation.TargetMethod))
                {
                    toLowerInvocation = invocation;

                    // Bail out if there is no parent invocation as we still need the string.Replace invocation.
                    if (!TryGetParentInvocation(invocation, out invocation!))
                    {
                        return false;
                    }
                }

                // Bail out if there is no appropriate string.Replace invocation.
                if (!IsAnyStringReplaceMethod(invocation.TargetMethod) ||
                    !HasStringReplaceArgumentsToRemoveHyphen(invocation.Arguments))
                {
                    return false;
                }

                outerInvocation = invocation;

                // Check for an optional string.ToLower invocation, e.g. in the case of
                // BitConverter.ToString(data).Replace("-", "").ToLower()
                if (TryGetParentInvocation(invocation, out invocation!))
                {
                    if (IsAnyStringToLowerMethod(invocation.TargetMethod))
                    {
                        toLowerInvocation = invocation;
                        outerInvocation = invocation;
                    }
                }

                // At this point we have a complete BitConverter.ToString invocation chain.
                // Get the appropriate replacement method from BitConverter.ToString to Convert.ToHexStringLower or Convert.ToHexString.
                if (toLowerInvocation is not null &&
                    _bitConverterReplacementsToLower.TryGetValue(bitConverterInvocation.TargetMethod, out var replacementToLower))
                {
                    replacementMethod = replacementToLower;
                    // Reset toLowerInvocation as we no longer need a string.ToLower after using Convert.ToHexStringLower.
                    toLowerInvocation = default;

                    return true;
                }
                else if (_bitConverterReplacements.TryGetValue(bitConverterInvocation.TargetMethod, out var replacement))
                {
                    replacementMethod = replacement;

                    return true;
                }

                // No replacement was found.
                return false;

                static bool TryGetParentInvocation(IInvocationOperation invocation, [NotNullWhen(true)] out IInvocationOperation? parentInvocation)
                {
                    parentInvocation = invocation.Parent as IInvocationOperation;

                    return parentInvocation is not null;
                }
            }

            private bool IsAnyBitConverterMethod(IMethodSymbol method)
            {
                return _bitConverterReplacements.ContainsKey(method) || _bitConverterReplacementsToLower.ContainsKey(method);
            }

            private bool IsAnyStringReplaceMethod(IMethodSymbol? method)
            {
                return _stringReplaceMethods.Any(m => SymbolEqualityComparer.Default.Equals(m, method));
            }

            private bool IsAnyStringToLowerMethod(IMethodSymbol? method)
            {
                return _stringToLowerMethods.Any(m => SymbolEqualityComparer.Default.Equals(m, method));
            }

            private bool IsStringEmptyField(IOperation operation)
            {
                return operation is IFieldReferenceOperation fieldReferenceOperation &&
                    SymbolEqualityComparer.Default.Equals(fieldReferenceOperation.Field, _stringEmptyField);
            }

            private bool HasStringReplaceArgumentsToRemoveHyphen(ImmutableArray<IArgumentOperation> arguments)
            {
                var argumentsInParameterOrder = arguments.GetArgumentsInParameterOrder();
                var oldValue = argumentsInParameterOrder[0].Value;
                var newValue = argumentsInParameterOrder[1].Value;

                bool oldValueIsConstantHyphenString = oldValue.ConstantValue.HasValue &&
                    oldValue.ConstantValue.Value is string oldValueString &&
                    oldValueString.Equals("-", StringComparison.Ordinal);

                bool newValueIsConstantNullOrEmptyString = newValue.ConstantValue.HasValue &&
                    newValue.ConstantValue.Value is string newValueString &&
                    string.IsNullOrEmpty(newValueString);

                return oldValueIsConstantHyphenString &&
                    (newValueIsConstantNullOrEmptyString || newValue.HasNullConstantValue() || IsStringEmptyField(newValue));
            }

            private readonly ImmutableArray<IMethodSymbol> _stringReplaceMethods;
            private readonly ImmutableArray<IMethodSymbol> _stringToLowerMethods;
            private readonly IFieldSymbol? _stringEmptyField;
            private readonly ImmutableDictionary<IMethodSymbol, IMethodSymbol> _bitConverterReplacements;
            private readonly ImmutableDictionary<IMethodSymbol, IMethodSymbol> _bitConverterReplacementsToLower;
        }
    }
}
