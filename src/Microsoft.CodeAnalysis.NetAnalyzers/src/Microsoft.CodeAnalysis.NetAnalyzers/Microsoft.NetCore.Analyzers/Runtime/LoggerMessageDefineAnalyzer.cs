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
using Microsoft.CodeQuality.Analyzers;
using Microsoft.Extensions.Logging;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1727: <inheritdoc cref="LoggerMessageDiagnosticUsePascalCasedLogMessageTokensTitle"/>
    /// CA1848: <inheritdoc cref="LoggerMessageDiagnosticUseCompiledLogMessagesTitle"/>
    /// CA2253: <inheritdoc cref="LoggerMessageDiagnosticNumericsInFormatStringTitle"/>
    /// CA2254: <inheritdoc cref="LoggerMessageDiagnosticConcatenationInFormatStringTitle"/>
    /// CA2017: <inheritdoc cref="LoggerMessageDiagnosticFormatParameterCountMismatchTitle"/>
    /// CA2023: <inheritdoc cref="LoggerMessageDiagnosticMessageTemplateBracesMismatchTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class LoggerMessageDefineAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1727RuleId = "CA1727";
        internal const string CA1848RuleId = "CA1848";
        internal const string CA2253RuleId = "CA2253";
        internal const string CA2254RuleId = "CA2254";
        internal const string CA2017RuleId = "CA2017";
        internal const string CA2023RuleId = "CA2023";

        internal static readonly DiagnosticDescriptor CA1727Rule = DiagnosticDescriptorHelper.Create(CA1727RuleId,
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticUsePascalCasedLogMessageTokensTitle)),
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticUsePascalCasedLogMessageTokensMessage)),
                                                                         DiagnosticCategory.Naming,
                                                                         RuleLevel.IdeHidden_BulkConfigurable,
                                                                         description: CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticUsePascalCasedLogMessageTokensDescription)),
                                                                         isPortedFxCopRule: false,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: false);

        internal static readonly DiagnosticDescriptor CA1848Rule = DiagnosticDescriptorHelper.Create(CA1848RuleId,
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticUseCompiledLogMessagesTitle)),
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticUseCompiledLogMessagesMessage)),
                                                                         DiagnosticCategory.Performance,
                                                                         RuleLevel.IdeHidden_BulkConfigurable,
                                                                         description: CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticUseCompiledLogMessagesDescription)),
                                                                         isPortedFxCopRule: false,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: false);

        internal static readonly DiagnosticDescriptor CA2253Rule = DiagnosticDescriptorHelper.Create(CA2253RuleId,
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticNumericsInFormatStringTitle)),
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticNumericsInFormatStringMessage)),
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.IdeSuggestion,
                                                                         description: CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticNumericsInFormatStringDescription)),
                                                                         isPortedFxCopRule: false,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: false);

        internal static readonly DiagnosticDescriptor CA2254Rule = DiagnosticDescriptorHelper.Create(CA2254RuleId,
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticConcatenationInFormatStringTitle)),
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticConcatenationInFormatStringMessage)),
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.IdeSuggestion,
                                                                         description: CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticConcatenationInFormatStringDescription)),
                                                                         isPortedFxCopRule: false,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: false);

        internal static readonly DiagnosticDescriptor CA2017Rule = DiagnosticDescriptorHelper.Create(CA2017RuleId,
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticFormatParameterCountMismatchTitle)),
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticFormatParameterCountMismatchMessage)),
                                                                         DiagnosticCategory.Reliability,
                                                                         RuleLevel.BuildWarning,
                                                                         description: CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticFormatParameterCountMismatchDescription)),
                                                                         isPortedFxCopRule: false,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: false);

        internal static readonly DiagnosticDescriptor CA2023Rule = DiagnosticDescriptorHelper.Create(CA2023RuleId,
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticMessageTemplateBracesMismatchTitle)),
                                                                         CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticMessageTemplateBracesMismatchMessage)),
                                                                         DiagnosticCategory.Reliability,
                                                                         RuleLevel.BuildWarning,
                                                                         description: CreateLocalizableResourceString(nameof(LoggerMessageDiagnosticMessageTemplateBracesMismatchDescription)),
                                                                         isPortedFxCopRule: false,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(CA1727Rule, CA1848Rule, CA2253Rule, CA2254Rule, CA2017Rule, CA2023Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(context =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);

                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingLoggerExtensions, out var loggerExtensionsType) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingILogger, out var loggerType) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingLoggerMessage, out var loggerMessageType))
                {
                    return;
                }

                context.RegisterOperationAction(context => AnalyzeInvocation(context, loggerType, loggerExtensionsType, loggerMessageType), OperationKind.Invocation);

                if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftExtensionsLoggingLoggerMessageAttribute, out var loggerMessageAttributeType))
                {
                    context.RegisterSymbolAction(context => AnalyzeMethodSymbol(context, loggerMessageAttributeType), SymbolKind.Method);
                }
            });
        }

        private void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol loggerType, INamedTypeSymbol loggerExtensionsType, INamedTypeSymbol loggerMessageType)
        {
            var invocation = (IInvocationOperation)context.Operation;

            var methodSymbol = invocation.TargetMethod;
            var containingType = methodSymbol.ContainingType;
            bool usingLoggerExtensionsTypes = false;

            if (containingType.Equals(loggerExtensionsType, SymbolEqualityComparer.Default))
            {
                usingLoggerExtensionsTypes = true;
                context.ReportDiagnostic(invocation.CreateDiagnostic(CA1848Rule, methodSymbol.ToDisplayString(GetLanguageSpecificFormat(invocation))));
            }
            else if (
                !containingType.Equals(loggerType, SymbolEqualityComparer.Default) &&
                !containingType.Equals(loggerMessageType, SymbolEqualityComparer.Default))
            {
                return;
            }

            if (FindLogParameters(methodSymbol, out var messageArgument, out var paramsArgument))
            {
                var paramsCount = 0;
                IOperation? formatExpression = null;
                var argsIsArray = false;

                if (containingType.Equals(loggerMessageType, SymbolEqualityComparer.Default))
                {
                    // For LoggerMessage.Define, count type parameters on the invocation instead of arguments
                    paramsCount = methodSymbol.TypeParameters.Length;
                    var arg = invocation.Arguments.FirstOrDefault(argument =>
                    {
                        var parameter = argument.Parameter;
                        return SymbolEqualityComparer.Default.Equals(parameter, messageArgument);
                    });
                    formatExpression = arg?.Value;
                }
                else
                {
                    foreach (var argument in invocation.Arguments)
                    {
                        var parameter = argument.Parameter;
                        if (SymbolEqualityComparer.Default.Equals(parameter, messageArgument))
                        {
                            formatExpression = argument.Value;
                        }
                        else if (SymbolEqualityComparer.Default.Equals(parameter, paramsArgument))
                        {
                            var parameterType = parameter!.Type;
                            if (parameterType == null)
                            {
                                return;
                            }

                            if (argument.Value is IArrayCreationOperation arrayCreation)
                            {
                                paramsCount += arrayCreation.Initializer!.ElementValues.Length;
                            }
                            else
                            {
                                argsIsArray = true;
                                paramsCount++;
                            }
                        }
                    }
                }

                if (formatExpression is not null)
                {
                    AnalyzeFormatArgument(context, formatExpression, paramsCount, argsIsArray, usingLoggerExtensionsTypes, methodSymbol);
                }
            }
        }

        private void AnalyzeMethodSymbol(SymbolAnalysisContext context, INamedTypeSymbol loggerMessageAttributeType)
        {
            var method = (IMethodSymbol)context.Symbol;

            // Look for LoggerMessageAttribute on the method
            var attribute = method.GetAttribute(loggerMessageAttributeType);
            if (attribute == null)
                return;

            // Extract the message template from the attribute
            var messageValue = ExtractMessageFromAttribute(attribute);
            if (messageValue == null)
                return;

            // Analyze the message template for CA1727, CA2253, and CA2023 rules
            AnalyzeMessageTemplateFromSymbol(context, messageValue, method);
        }

        private string? ExtractMessageFromAttribute(AttributeData attribute)
        {
            // Check constructor arguments (positional) - typically: LoggerMessage(eventId, level, message)
            if (attribute.ConstructorArguments.Length >= 3 &&
                attribute.ConstructorArguments[2].Value is string constructorMessage)
            {
                return constructorMessage;
            }

            // Check named arguments - Message = "..."
            var messageArg = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Message");
            if (messageArg.Value.Value is string namedMessage)
            {
                return namedMessage;
            }

            return null;
        }

        private void AnalyzeMessageTemplateFromSymbol(SymbolAnalysisContext context, string text, IMethodSymbol methodSymbol)
        {
            // Get the first syntax reference for reporting location
            var syntaxReference = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxReference == null)
                return;

            var location = syntaxReference.GetSyntax().GetLocation();

            // Use the common analysis logic but report diagnostics directly to the symbol context
            AnalyzeMessageTemplateCore(text,
                onInvalidTemplate: () => context.ReportDiagnostic(Diagnostic.Create(CA2023Rule, location)),
                onNumericPlaceholder: () => context.ReportDiagnostic(Diagnostic.Create(CA2253Rule, location)),
                onCamelCasePlaceholder: () => context.ReportDiagnostic(Diagnostic.Create(CA1727Rule, location)),
                onParameterCountMismatch: null); // Skip parameter count validation for LoggerMessageAttribute methods
        }

        private void AnalyzeFormatArgument(OperationAnalysisContext context, IOperation formatExpression, int paramsCount, bool argsIsArray, bool usingLoggerExtensionsTypes, IMethodSymbol methodSymbol)
        {
            var text = TryGetFormatText(formatExpression);
            if (text == null)
            {
                if (usingLoggerExtensionsTypes)
                {
                    context.ReportDiagnostic(formatExpression.CreateDiagnostic(CA2254Rule, methodSymbol.ToDisplayString(GetLanguageSpecificFormat(formatExpression))));
                }

                return;
            }

            // Use the common analysis logic but report diagnostics to the operation context
            AnalyzeMessageTemplateCore(text,
                onInvalidTemplate: () => context.ReportDiagnostic(formatExpression.CreateDiagnostic(CA2023Rule)),
                onNumericPlaceholder: () => context.ReportDiagnostic(formatExpression.CreateDiagnostic(CA2253Rule)),
                onCamelCasePlaceholder: () => context.ReportDiagnostic(formatExpression.CreateDiagnostic(CA1727Rule)),
                onParameterCountMismatch: (expectedCount) =>
                {
                    var argsPassedDirectly = argsIsArray && paramsCount == 1;
                    if (!argsPassedDirectly && paramsCount != expectedCount)
                    {
                        context.ReportDiagnostic(formatExpression.CreateDiagnostic(CA2017Rule));
                    }
                });
        }

        /// <summary>
        /// Core logic for analyzing message templates. Uses callbacks to report diagnostics to different contexts.
        /// </summary>
        private void AnalyzeMessageTemplateCore(string text, Action onInvalidTemplate, Action onNumericPlaceholder, Action onCamelCasePlaceholder, Action<int>? onParameterCountMismatch)
        {
            LogValuesFormatter formatter;
            try
            {
                formatter = new LogValuesFormatter(text);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return;
            }

            if (!IsValidMessageTemplate(formatter.OriginalFormat))
            {
                onInvalidTemplate();
                return;
            }

            foreach (var valueName in formatter.ValueNames)
            {
                if (int.TryParse(valueName, out _))
                {
                    onNumericPlaceholder();
                }
                else if (!string.IsNullOrEmpty(valueName) && char.IsLower(valueName[0]))
                {
                    onCamelCasePlaceholder();
                }
            }

            // Parameter count validation (only for operation-based analysis)
            onParameterCountMismatch?.Invoke(formatter.ValueNames.Count);
        }

        private static SymbolDisplayFormat GetLanguageSpecificFormat(IOperation operation) =>
            operation.Language == LanguageNames.CSharp ?
                SymbolDisplayFormat.CSharpShortErrorMessageFormat : SymbolDisplayFormat.VisualBasicShortErrorMessageFormat;

        private string? TryGetFormatText(IOperation? argumentExpression)
        {
            if (argumentExpression is null)
                return null;

            switch (argumentExpression)
            {
                case IOperation { ConstantValue: { HasValue: true, Value: string constantValue } }:
                    return constantValue;
                case IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } binary:
                    var leftText = TryGetFormatText(binary.LeftOperand);
                    var rightText = TryGetFormatText(binary.RightOperand);

                    if (leftText != null && rightText != null)
                    {
                        return leftText + rightText;
                    }

                    return null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Is the message template valid? (no unclosed braces, no braces without an opening, and no unescaped braces)
        /// </summary>
        /// <param name="messageTemplate">The message template to check for validity.</param>
        /// <returns>When true braces are valid, false otherwise.</returns>
        public static bool IsValidMessageTemplate(string messageTemplate)
        {
            if (messageTemplate is null)
            {
                return false;
            }

            int index = 0;
            bool leftBrace = false;

            while (index < messageTemplate.Length)
            {
                if (messageTemplate[index] == '{')
                {
                    if (index < messageTemplate.Length - 1 && messageTemplate[index + 1] == '{')
                    {
                        index++;
                    }
                    else if (leftBrace)
                    {
                        return false;
                    }
                    else
                    {
                        leftBrace = true;
                    }
                }
                else if (messageTemplate[index] == '}')
                {
                    if (leftBrace)
                    {
                        leftBrace = false;
                    }
                    else if (index < messageTemplate.Length - 1 && messageTemplate[index + 1] == '}')
                    {
                        index++;
                    }
                    else
                    {
                        return false;
                    }
                }

                index++;
            }

            return !leftBrace;
        }

        private static bool FindLogParameters(IMethodSymbol methodSymbol, [NotNullWhen(true)] out IParameterSymbol? message, out IParameterSymbol? arguments)
        {
            message = null;
            arguments = null;
            foreach (var parameter in methodSymbol.Parameters)
            {
                if (parameter.Type.SpecialType == SpecialType.System_String &&
                    (string.Equals(parameter.Name, "message", StringComparison.Ordinal) ||
                    string.Equals(parameter.Name, "messageFormat", StringComparison.Ordinal) ||
                    string.Equals(parameter.Name, "formatString", StringComparison.Ordinal)))
                {
                    message = parameter;
                }
                // When calling logger.BeginScope("{Param}") generic overload would be selected
                else if (parameter.Type.SpecialType == SpecialType.System_String &&
                    methodSymbol.Name.Equals("BeginScope") &&
                    string.Equals(parameter.Name, "state", StringComparison.Ordinal))
                {
                    message = parameter;
                }
                else if (parameter.IsParams &&
                    string.Equals(parameter.Name, "args", StringComparison.Ordinal))
                {
                    arguments = parameter;
                }
            }

            return message != null;
        }
    }
}
