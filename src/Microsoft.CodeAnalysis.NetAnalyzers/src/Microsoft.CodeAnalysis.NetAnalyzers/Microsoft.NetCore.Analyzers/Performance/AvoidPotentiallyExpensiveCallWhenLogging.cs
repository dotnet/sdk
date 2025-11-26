// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1873: <inheritdoc cref="AvoidPotentiallyExpensiveCallWhenLoggingTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidPotentiallyExpensiveCallWhenLoggingAnalyzer : DiagnosticAnalyzer
    {
        private const string RuleId = "CA1873";

        private const string Level = nameof(Level);
        private const string LogLevel = nameof(LogLevel);

        private const string Log = nameof(Log);
        private const string IsEnabled = nameof(IsEnabled);
        private const string LogTrace = nameof(LogTrace);
        private const string LogDebug = nameof(LogDebug);
        private const string LogInformation = nameof(LogInformation);
        private const string LogWarning = nameof(LogWarning);
        private const string LogError = nameof(LogError);
        private const string LogCritical = nameof(LogCritical);

        private const int LogLevelTrace = 0;
        private const int LogLevelDebug = 1;
        private const int LogLevelInformation = 2;
        private const int LogLevelWarning = 3;
        private const int LogLevelError = 4;
        private const int LogLevelCritical = 5;
        private const int LogLevelPassedAsParameter = int.MinValue;

        private static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidPotentiallyExpensiveCallWhenLoggingTitle)),
            CreateLocalizableResourceString(nameof(AvoidPotentiallyExpensiveCallWhenLoggingMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(AvoidPotentiallyExpensiveCallWhenLoggingDescription)),
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

                // Check if we have a log invocation and capture the log level used, either as IOperation or as int.
                // Then, check if the invocation is guarded by 'ILogger.IsEnabled' and bail out if it is.
                if (!symbols.IsLogInvocation(invocation, out var logLevel, out var logLevelArgumentOperation) ||
                    symbols.IsGuardedByIsEnabled(invocation, logLevel, logLevelArgumentOperation))
                {
                    return;
                }

                var arguments = invocation.Arguments.Skip(invocation.IsExtensionMethodAndHasNoInstance() ? 1 : 0);

                // Check each argument if it is potentially expensive to evaluate and raise a diagnostic if it is.
                foreach (var argument in arguments)
                {
                    if (IsPotentiallyExpensive(argument.Value))
                    {
                        context.ReportDiagnostic(argument.CreateDiagnostic(Rule));
                    }
                }
            }
        }

        private static bool IsPotentiallyExpensive(IOperation? operation)
        {
            if (operation is null)
            {
                return false;
            }

            if (ICollectionExpressionOperationWrapper.IsInstance(operation) ||
                operation is IAnonymousObjectCreationOperation or
                IAwaitOperation or
                IInvocationOperation or
                IObjectCreationOperation { Type.IsReferenceType: true } or
                IWithOperation)
            {
                return true;
            }

            if (operation is IArrayCreationOperation arrayCreationOperation)
            {
                return !IsEmptyImplicitParamsArrayCreation(arrayCreationOperation);
            }

            if (operation is IConversionOperation conversionOperation)
            {
                return IsBoxing(conversionOperation) || IsPotentiallyExpensive(conversionOperation.Operand);
            }

            if (operation is IArrayElementReferenceOperation arrayElementReferenceOperation)
            {
                return IsPotentiallyExpensive(arrayElementReferenceOperation.ArrayReference) ||
                    arrayElementReferenceOperation.Indices.Any(IsPotentiallyExpensive);
            }

            if (operation is IBinaryOperation binaryOperation)
            {
                return IsPotentiallyExpensive(binaryOperation.LeftOperand) ||
                    IsPotentiallyExpensive(binaryOperation.RightOperand);
            }

            if (operation is ICoalesceOperation coalesceOperation)
            {
                return IsPotentiallyExpensive(coalesceOperation.Value) ||
                    IsPotentiallyExpensive(coalesceOperation.WhenNull);
            }

            if (operation is IConditionalAccessOperation conditionalAccessOperation)
            {
                return IsPotentiallyExpensive(conditionalAccessOperation.WhenNotNull);
            }

            if (operation is IIncrementOrDecrementOperation incrementOrDecrementOperation)
            {
                return IsPotentiallyExpensive(incrementOrDecrementOperation.Target);
            }

            if (operation is IInterpolatedStringOperation interpolatedStringOperation)
            {
                return interpolatedStringOperation.Parts.Any(p => p is
                    IInterpolationOperation { Expression.ConstantValue.HasValue: false } or
                    IInterpolatedStringTextOperation { Text.ConstantValue.HasValue: false });
            }

            if (operation is IMemberReferenceOperation memberReferenceOperation)
            {
                if (IsPotentiallyExpensive(memberReferenceOperation.Instance))
                {
                    return true;
                }

                if (memberReferenceOperation is IPropertyReferenceOperation { Arguments.IsEmpty: false } indexerReferenceOperation)
                {
                    return indexerReferenceOperation.Arguments.Any(a => IsPotentiallyExpensive(a.Value));
                }
            }

            if (operation is IUnaryOperation unaryOperation)
            {
                return IsPotentiallyExpensive(unaryOperation.Operand);
            }

            return false;

            static bool IsBoxing(IConversionOperation conversionOperation)
            {
                var targetIsReferenceType = conversionOperation.Type?.IsReferenceType ?? false;
                var operandIsValueType = conversionOperation.Operand.Type?.IsValueType ?? false;

                return targetIsReferenceType && operandIsValueType;
            }

            static bool IsEmptyImplicitParamsArrayCreation(IArrayCreationOperation arrayCreationOperation)
            {
                return arrayCreationOperation.IsImplicit &&
                    arrayCreationOperation.DimensionSizes.Length == 1 &&
                    arrayCreationOperation.DimensionSizes[0].ConstantValue.HasValue &&
                    arrayCreationOperation.DimensionSizes[0].ConstantValue.Value is int size &&
                    size == 0;
            }
        }

        internal sealed class RequiredSymbols
        {
            private RequiredSymbols(
                IMethodSymbol logMethod,
                IMethodSymbol isEnabledMethod,
                ImmutableDictionary<IMethodSymbol, int> logExtensionsMethodsAndLevel,
                INamedTypeSymbol? loggerMessageAttributeType)
            {
                _logMethod = logMethod;
                _isEnabledMethod = isEnabledMethod;
                _logExtensionsMethodsAndLevel = logExtensionsMethodsAndLevel;
                _loggerMessageAttributeType = loggerMessageAttributeType;
            }

            public static bool TryGetSymbols(Compilation compilation, [NotNullWhen(true)] out RequiredSymbols? symbols)
            {
                symbols = default;

                var iLoggerType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingILogger);

                if (iLoggerType is null)
                {
                    return false;
                }

                var logMethod = iLoggerType.GetMembers(Log)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault();

                var isEnabledMethod = iLoggerType.GetMembers(IsEnabled)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault();

                if (logMethod is null || isEnabledMethod is null)
                {
                    return false;
                }

                var loggerExtensionsType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingLoggerExtensions);
                var logExtensionsMethodsBuilder = ImmutableDictionary.CreateBuilder<IMethodSymbol, int>(SymbolEqualityComparer.Default);
                AddRangeIfNotNull(logExtensionsMethodsBuilder, loggerExtensionsType?.GetMembers(LogTrace).OfType<IMethodSymbol>(), LogLevelTrace);
                AddRangeIfNotNull(logExtensionsMethodsBuilder, loggerExtensionsType?.GetMembers(LogDebug).OfType<IMethodSymbol>(), LogLevelDebug);
                AddRangeIfNotNull(logExtensionsMethodsBuilder, loggerExtensionsType?.GetMembers(LogInformation).OfType<IMethodSymbol>(), LogLevelInformation);
                AddRangeIfNotNull(logExtensionsMethodsBuilder, loggerExtensionsType?.GetMembers(LogWarning).OfType<IMethodSymbol>(), LogLevelWarning);
                AddRangeIfNotNull(logExtensionsMethodsBuilder, loggerExtensionsType?.GetMembers(LogError).OfType<IMethodSymbol>(), LogLevelError);
                AddRangeIfNotNull(logExtensionsMethodsBuilder, loggerExtensionsType?.GetMembers(LogCritical).OfType<IMethodSymbol>(), LogLevelCritical);
                AddRangeIfNotNull(logExtensionsMethodsBuilder, loggerExtensionsType?.GetMembers(Log).OfType<IMethodSymbol>(), LogLevelPassedAsParameter);

                var loggerMessageAttributeType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingLoggerMessageAttribute);

                symbols = new RequiredSymbols(logMethod, isEnabledMethod, logExtensionsMethodsBuilder.ToImmutable(), loggerMessageAttributeType);

                return true;

                void AddRangeIfNotNull(ImmutableDictionary<IMethodSymbol, int>.Builder builder, IEnumerable<IMethodSymbol>? range, int value)
                {
                    if (range is not null)
                    {
                        builder.AddRange(range.Select(s => new KeyValuePair<IMethodSymbol, int>(s, value)));
                    }
                }
            }

            public bool IsLogInvocation(IInvocationOperation invocation, out int logLevel, out IArgumentOperation? logLevelArgumentOperation)
            {
                logLevel = LogLevelPassedAsParameter;
                logLevelArgumentOperation = default;

                var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;

                // ILogger.Log
                if (SymbolEqualityComparer.Default.Equals(method.ConstructedFrom, _logMethod) ||
                    method.ConstructedFrom.IsOverrideOrImplementationOfInterfaceMember(_logMethod))
                {
                    logLevelArgumentOperation = invocation.Arguments.GetArgumentForParameterAtIndex(0);

                    return true;
                }

                // LoggerExtensions.Log and named variants (e.g. LoggerExtensions.LogInformation)
                if (_logExtensionsMethodsAndLevel.TryGetValue(method, out logLevel))
                {
                    // LoggerExtensions.Log
                    if (logLevel == LogLevelPassedAsParameter)
                    {
                        logLevelArgumentOperation = invocation.Arguments.GetArgumentForParameterAtIndex(invocation.IsExtensionMethodAndHasNoInstance() ? 1 : 0);
                    }

                    return true;
                }

                var loggerMessageAttribute = method.GetAttribute(_loggerMessageAttributeType);

                if (loggerMessageAttribute is null)
                {
                    return false;
                }

                // Try to get the log level from the attribute arguments.
                logLevel = loggerMessageAttribute.NamedArguments
                    .FirstOrDefault(p => p.Key.Equals(Level, StringComparison.Ordinal))
                    .Value.Value as int?
                    ?? LogLevelPassedAsParameter;

                if (logLevel == LogLevelPassedAsParameter)
                {
                    logLevelArgumentOperation = invocation.Arguments
                        .FirstOrDefault(a => a.Value.Type?.Name.Equals(LogLevel, StringComparison.Ordinal) ?? false);

                    if (logLevelArgumentOperation is null)
                    {
                        return false;
                    }
                }

                return true;
            }

            public bool IsGuardedByIsEnabled(IInvocationOperation logInvocation, int logLevel, IArgumentOperation? logLevelArgumentOperation)
            {
                // Check each block for conditionals that contain an 'ILogger.IsEnabled' invocation that guards the log invocation:
                //   1. If the 'ILogger.IsEnabled' invocation is negated, the 'WhenTrue' branch must contain a return.
                //   2. If the 'ILogger.IsEnabled' invocation is not negated, the 'WhenTrue' branch must contain the log invocation.
                // This is also not perfect, but should be good enough to prevent false positives.
                var currentBlockAncestor = logInvocation.GetAncestor<IBlockOperation>(OperationKind.Block);
                while (currentBlockAncestor is not null)
                {
                    var guardConditionals = currentBlockAncestor.Descendants().OfType<IConditionalOperation>();
                    if (guardConditionals.Any(IsValidGuardConditional))
                    {
                        return true;
                    }

                    currentBlockAncestor = currentBlockAncestor.GetAncestor<IBlockOperation>(OperationKind.Block);
                }

                return false;

                bool IsValidGuardConditional(IConditionalOperation conditional)
                {
                    if (conditional.Syntax.SpanStart > logInvocation.Syntax.SpanStart)
                    {
                        return false;
                    }

                    var conditionInvocations = conditional.Condition
                        .DescendantsAndSelf()
                        .OfType<IInvocationOperation>();

                    if (conditionInvocations.Any(IsValidIsEnabledGuardInvocation))
                    {
                        return true;
                    }

                    return false;

                    bool IsValidIsEnabledGuardInvocation(IInvocationOperation invocation)
                    {
                        if (!IsIsEnabledInvocation(invocation) ||
                            !AreInvocationsOnSameInstance(logInvocation, invocation) ||
                            !IsSameLogLevel(invocation.Arguments[0]))
                        {
                            return false;
                        }

                        var isNegated = invocation.Parent is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not };
                        var descendants = conditional.WhenTrue.DescendantsAndSelf();

                        return isNegated && descendants.OfType<IReturnOperation>().Any() || !isNegated && descendants.Contains(logInvocation);
                    }
                }

                bool IsIsEnabledInvocation(IInvocationOperation invocation)
                {
                    return SymbolEqualityComparer.Default.Equals(_isEnabledMethod, invocation.TargetMethod) ||
                        invocation.TargetMethod.IsOverrideOrImplementationOfInterfaceMember(_isEnabledMethod);
                }

                static bool AreInvocationsOnSameInstance(IInvocationOperation invocation1, IInvocationOperation invocation2)
                {
                    return SymbolEqualityComparer.Default.Equals(
                        GetInstanceResolvingConditionalAccess(invocation1).GetReferencedMemberOrLocalOrParameter(),
                        GetInstanceResolvingConditionalAccess(invocation2).GetReferencedMemberOrLocalOrParameter());
                }

                static IOperation? GetInstanceResolvingConditionalAccess(IInvocationOperation invocation)
                {
                    var instance = invocation.GetInstance()?.WalkDownConversion();

                    if (instance is IConditionalAccessInstanceOperation conditionalAccessInstance)
                    {
                        return conditionalAccessInstance.GetConditionalAccess()?.Operation;
                    }

                    return instance;
                }

                bool IsSameLogLevel(IArgumentOperation isEnabledArgument)
                {
                    if (isEnabledArgument.Value.ConstantValue.HasValue)
                    {
                        int isEnabledLogLevel = (int)isEnabledArgument.Value.ConstantValue.Value!;

                        return logLevel == LogLevelPassedAsParameter
                            ? logLevelArgumentOperation?.Value.HasConstantValue(isEnabledLogLevel) ?? false
                            : isEnabledLogLevel == logLevel;
                    }

                    return SymbolEqualityComparer.Default.Equals(
                        isEnabledArgument.Value.GetReferencedMemberOrLocalOrParameter(),
                        logLevelArgumentOperation?.Value.GetReferencedMemberOrLocalOrParameter());
                }
            }

            private readonly IMethodSymbol _logMethod;
            private readonly IMethodSymbol _isEnabledMethod;
            private readonly ImmutableDictionary<IMethodSymbol, int> _logExtensionsMethodsAndLevel;
            private readonly INamedTypeSymbol? _loggerMessageAttributeType;
        }
    }
}
