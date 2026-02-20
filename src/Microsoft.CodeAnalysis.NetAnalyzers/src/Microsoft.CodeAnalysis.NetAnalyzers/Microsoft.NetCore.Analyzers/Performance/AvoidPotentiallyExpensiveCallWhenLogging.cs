// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
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

        private const int LogLevelTrace = 0; // LogLevel.Trace
        private const int LogLevelDebug = 1; // LogLevel.Debug
        private const int LogLevelInformation = 2; // LogLevel.Information
        private const int LogLevelWarning = 3; // LogLevel.Warning
        private const int LogLevelError = 4; // LogLevel.Error
        private const int LogLevelCritical = 5; // LogLevel.Critical
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

        private static readonly Dictionary<string, int> s_logLevelsByName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["trace"] = LogLevelTrace,
            ["debug"] = LogLevelDebug,
            ["information"] = LogLevelInformation,
            ["warning"] = LogLevelWarning,
            ["error"] = LogLevelError,
            ["critical"] = LogLevelCritical,
        };

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (RequiredSymbols.GetSymbols(context.Compilation) is not { } symbols)
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

                // Check if the log level exceeds the configured maximum threshold.
                if (logLevel != LogLevelPassedAsParameter &&
                    logLevel < LogLevelCritical &&
                    logLevel > ParseLogLevel(context.Options.GetStringOptionValue(EditorConfigOptionNames.MaxLogLevel, Rule, invocation.Syntax.SyntaxTree, context.Compilation)))
                {
                    return;
                }

                // Check each argument if it is potentially expensive to evaluate and raise a diagnostic if it is.
                foreach (var argument in invocation.Arguments.Skip(invocation.IsExtensionMethodAndHasNoInstance() ? 1 : 0))
                {
                    if (GetExpenseReason(argument.Value) is { } reason)
                    {
                        context.ReportDiagnostic(argument.CreateDiagnostic(Rule, reason));
                    }
                }

                static int ParseLogLevel(string? logLevelString) =>
                    logLevelString is not null && s_logLevelsByName.TryGetValue(logLevelString, out var level) ? level :
                    LogLevelInformation; // Default to Information if invalid
            }
        }

        private static string? GetExpenseReason(IOperation? operation)
        {
            if (operation is null)
            {
                return null;
            }

            if (ICollectionExpressionOperationWrapper.IsInstance(operation))
            {
                return MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonCollectionExpression;
            }

            if (operation is IAnonymousObjectCreationOperation)
            {
                return MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonAnonymousObjectCreation;
            }

            if (operation is IAwaitOperation)
            {
                return MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonAwaitExpression;
            }

            if (operation is IWithOperation)
            {
                return MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonWithExpression;
            }

            if (operation is IInvocationOperation invocationOperation)
            {
                return !IsTrivialInvocation(invocationOperation)
                    ? MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonMethodInvocation
                    : null;
            }

            if (operation is IObjectCreationOperation { Type.IsReferenceType: true })
            {
                return MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonObjectCreation;
            }

            if (operation is IArrayCreationOperation arrayCreationOperation)
            {
                return !IsEmptyImplicitParamsArrayCreation(arrayCreationOperation)
                    ? MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonArrayCreation
                    : null;
            }

            if (operation is IConversionOperation conversionOperation)
            {
                return IsBoxing(conversionOperation)
                    ? MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonBoxingConversion
                    : GetExpenseReason(conversionOperation.Operand);
            }

            if (operation is IArrayElementReferenceOperation arrayElementReferenceOperation)
            {
                return GetExpenseReason(arrayElementReferenceOperation.ArrayReference) ??
                       arrayElementReferenceOperation.Indices.Select(GetExpenseReason).FirstOrDefault(r => r is not null);
            }

            if (operation is IBinaryOperation binaryOperation)
            {
                return GetExpenseReason(binaryOperation.LeftOperand) ??
                       GetExpenseReason(binaryOperation.RightOperand);
            }

            if (operation is ICoalesceOperation coalesceOperation)
            {
                return GetExpenseReason(coalesceOperation.Value) ??
                       GetExpenseReason(coalesceOperation.WhenNull);
            }

            if (operation is IConditionalAccessOperation conditionalAccessOperation)
            {
                return GetExpenseReason(conditionalAccessOperation.WhenNotNull);
            }

            if (operation is IIncrementOrDecrementOperation incrementOrDecrementOperation)
            {
                return GetExpenseReason(incrementOrDecrementOperation.Target);
            }

            if (operation is IInterpolatedStringOperation interpolatedStringOperation)
            {
                return interpolatedStringOperation.Parts.Any(p => p is
                    IInterpolationOperation { Expression.ConstantValue.HasValue: false } or
                    IInterpolatedStringTextOperation { Text.ConstantValue.HasValue: false })
                    ? MicrosoftNetCoreAnalyzersResources.AvoidPotentiallyExpensiveCallWhenLoggingReasonStringInterpolation
                    : null;
            }

            if (operation is IMemberReferenceOperation memberReferenceOperation)
            {
                var instanceReason = GetExpenseReason(memberReferenceOperation.Instance);
                if (instanceReason is not null)
                {
                    return instanceReason;
                }

                if (memberReferenceOperation is IPropertyReferenceOperation propertyReferenceOperation)
                {
                    // We assume simple property accesses are cheap. For properties with arguments (indexers),
                    // we do still need to validate the arguments.
                    return propertyReferenceOperation.Arguments.Select(static a => GetExpenseReason(a.Value)).FirstOrDefault(r => r is not null);
                }
            }

            if (operation is IUnaryOperation unaryOperation)
            {
                return GetExpenseReason(unaryOperation.Operand);
            }

            return null;

            static bool IsTrivialInvocation(IInvocationOperation invocationOperation)
            {
                var method = invocationOperation.TargetMethod;

                // Special-case methods that are cheap enough we don't need to warn and
                // that are reasonably common as arguments to logging methods.

                // object.GetType / object.GetHashCode
                if (method.ContainingType?.SpecialType == SpecialType.System_Object &&
                    method.Parameters.IsEmpty &&
                    (method.Name is nameof(GetType) or nameof(GetHashCode)))
                {
                    return true;
                }

                // Stopwatch.GetTimestamp
                if (method.Name == nameof(Stopwatch.GetTimestamp) &&
                    method.IsStatic &&
                    method.Parameters.IsEmpty &&
                    method.ContainingType?.ToDisplayString() == "System.Diagnostics.Stopwatch")
                {
                    return true;
                }

                return false;
            }

            static bool IsBoxing(IConversionOperation conversionOperation) =>
                conversionOperation.Type?.IsReferenceType is true &&
                conversionOperation.Operand.Type?.IsValueType is true;

            static bool IsEmptyImplicitParamsArrayCreation(IArrayCreationOperation arrayCreationOperation) =>
                arrayCreationOperation.IsImplicit &&
                arrayCreationOperation.DimensionSizes.Length == 1 &&
                arrayCreationOperation.DimensionSizes[0].ConstantValue.HasValue &&
                arrayCreationOperation.DimensionSizes[0].ConstantValue.Value is int size &&
                size == 0;
        }

        internal sealed class RequiredSymbols(
            IMethodSymbol logMethod,
            IMethodSymbol isEnabledMethod,
            Dictionary<IMethodSymbol, int> logExtensionsMethodsAndLevel,
            INamedTypeSymbol? loggerMessageAttributeType)
        {
            private readonly IMethodSymbol _logMethod = logMethod;
            private readonly IMethodSymbol _isEnabledMethod = isEnabledMethod;
            private readonly Dictionary<IMethodSymbol, int> _logExtensionsMethodsAndLevel = logExtensionsMethodsAndLevel;
            private readonly INamedTypeSymbol? _loggerMessageAttributeType = loggerMessageAttributeType;

            public static RequiredSymbols? GetSymbols(Compilation compilation)
            {
                if (compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingILogger) is not { } iLoggerType ||
                    iLoggerType.GetMembers("Log").OfType<IMethodSymbol>().FirstOrDefault() is not { } logMethod ||
                    iLoggerType.GetMembers("IsEnabled").OfType<IMethodSymbol>().FirstOrDefault() is not { } isEnabledMethod)
                {
                    return null;
                }

                Dictionary<IMethodSymbol, int> logExtensionsMethods = new(SymbolEqualityComparer.Default);
                if (compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingLoggerExtensions) is { } loggerExtensionsType)
                {
                    foreach (var m in loggerExtensionsType.GetMembers().OfType<IMethodSymbol>())
                    {
                        switch (m.Name)
                        {
                            case "LogTrace": logExtensionsMethods[m] = LogLevelTrace; break;
                            case "LogDebug": logExtensionsMethods[m] = LogLevelDebug; break;
                            case "LogInformation": logExtensionsMethods[m] = LogLevelInformation; break;
                            case "LogWarning": logExtensionsMethods[m] = LogLevelWarning; break;
                            case "LogError": logExtensionsMethods[m] = LogLevelError; break;
                            case "LogCritical": logExtensionsMethods[m] = LogLevelCritical; break;
                            case "Log": logExtensionsMethods[m] = LogLevelPassedAsParameter; break;
                        }
                    }
                }

                return new RequiredSymbols(
                    logMethod,
                    isEnabledMethod,
                    logExtensionsMethods,
                    compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingLoggerMessageAttribute));
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

                    if (logLevelArgumentOperation?.Value.ConstantValue.HasValue == true)
                    {
                        logLevel = (int)logLevelArgumentOperation.Value.ConstantValue.Value!;
                    }

                    return true;
                }

                // LoggerExtensions.Log and named variants (e.g. LoggerExtensions.LogInformation)
                if (_logExtensionsMethodsAndLevel.TryGetValue(method, out logLevel))
                {
                    // LoggerExtensions.Log
                    if (logLevel == LogLevelPassedAsParameter)
                    {
                        logLevelArgumentOperation = invocation.Arguments.GetArgumentForParameterAtIndex(invocation.IsExtensionMethodAndHasNoInstance() ? 1 : 0);

                        if (logLevelArgumentOperation?.Value.ConstantValue.HasValue == true)
                        {
                            logLevel = (int)logLevelArgumentOperation.Value.ConstantValue.Value!;
                        }
                    }

                    return true;
                }

                if (method.GetAttribute(_loggerMessageAttributeType) is not { } loggerMessageAttribute)
                {
                    return false;
                }

                // Try to get the log level from the attribute arguments.
                logLevel = loggerMessageAttribute.NamedArguments
                    .FirstOrDefault(p => p.Key.Equals("Level", StringComparison.Ordinal))
                    .Value.Value as int?
                    ?? LogLevelPassedAsParameter;

                if (logLevel == LogLevelPassedAsParameter)
                {
                    logLevelArgumentOperation = invocation.Arguments
                        .FirstOrDefault(a => a.Value.Type?.Name.Equals("LogLevel", StringComparison.Ordinal) ?? false);

                    if (logLevelArgumentOperation is null)
                    {
                        return false;
                    }

                    if (logLevelArgumentOperation.Value.ConstantValue.HasValue)
                    {
                        logLevel = (int)logLevelArgumentOperation.Value.ConstantValue.Value!;
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
        }
    }
}
