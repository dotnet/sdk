// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers;
using Microsoft.Extensions.Logging;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LoggerMessageDefineAnalyzer : DiagnosticAnalyzer
    {
        internal const string CA1727RuleId = "CA1727";
        internal const string CA1848RuleId = "CA1848";
        internal const string CA2253RuleId = "CA2253";
        internal const string CA2254RuleId = "CA2254";
        internal const string CA2255RuleId = "CA2255";

        private static readonly LocalizableString s_localizableTitleCA1727 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticUsePascalCasedLogMessageTokensTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1727 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticUsePascalCasedLogMessageTokensMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1727 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticUsePascalCasedLogMessageTokensDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA1848 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticUseCompiledLogMessagesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA1848 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticUseCompiledLogMessagesMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA1848 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticUseCompiledLogMessagesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA2253 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticNumericsInFormatStringTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA2253 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticNumericsInFormatStringMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA2253 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticNumericsInFormatStringDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA2254 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticConcatenationInFormatStringTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA2254 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticConcatenationInFormatStringMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA2254 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticConcatenationInFormatStringDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableTitleCA2255 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticFormatParameterCountMismatchTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCA2255 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticFormatParameterCountMismatchMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescriptionCA2255 = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.LoggerMessageDiagnosticFormatParameterCountMismatchDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor CA1727Rule = DiagnosticDescriptorHelper.Create(CA1727RuleId,
                                                                         s_localizableTitleCA1727,
                                                                         s_localizableMessageCA1727,
                                                                         DiagnosticCategory.Naming,
                                                                         RuleLevel.Disabled,
                                                                         description: s_localizableDescriptionCA1727,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false,
                                                                         // isEnabledByDefaultInAggressiveMode: false,
                                                                         isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor CA1848Rule = DiagnosticDescriptorHelper.Create(CA1848RuleId,
                                                                         s_localizableTitleCA1848,
                                                                         s_localizableMessageCA1848,
                                                                         DiagnosticCategory.Performance,
                                                                         RuleLevel.Disabled,
                                                                         description: s_localizableDescriptionCA1848,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false,
                                                                         // isEnabledByDefaultInAggressiveMode: false,
                                                                         isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor CA2253Rule = DiagnosticDescriptorHelper.Create(CA2253RuleId,
                                                                         s_localizableTitleCA2253,
                                                                         s_localizableMessageCA2253,
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.IdeSuggestion,
                                                                         description: s_localizableDescriptionCA2253,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor CA2254Rule = DiagnosticDescriptorHelper.Create(CA2254RuleId,
                                                                         s_localizableTitleCA2254,
                                                                         s_localizableMessageCA2254,
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.IdeSuggestion,
                                                                         description: s_localizableDescriptionCA2254,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: true);

        internal static DiagnosticDescriptor CA2255Rule = DiagnosticDescriptorHelper.Create(CA2255RuleId,
                                                                         s_localizableTitleCA2255,
                                                                         s_localizableMessageCA2255,
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.BuildWarning,
                                                                         description: s_localizableDescriptionCA2255,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false,
                                                                         isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(CA1727Rule, CA1848Rule, CA2253Rule, CA2254Rule, CA2255Rule);

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
            });
        }

        private void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol loggerType, INamedTypeSymbol loggerExtensionsType, INamedTypeSymbol loggerMessageType)
        {
            var invocation = (IInvocationOperation)context.Operation;

            var methodSymbol = invocation.TargetMethod;
            var containingType = methodSymbol.ContainingType;

            if (containingType.Equals(loggerExtensionsType, SymbolEqualityComparer.Default))
            {
                context.ReportDiagnostic(Diagnostic.Create(CA1848Rule, invocation.Syntax.GetLocation(), methodSymbol.Name));
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
                        return parameter.Equals(messageArgument, SymbolEqualityComparer.Default);
                    });
                    formatExpression = arg.Value;
                }
                else
                {
                    foreach (var argument in invocation.Arguments)
                    {
                        var parameter = argument.Parameter;
                        if (parameter.Equals(messageArgument, SymbolEqualityComparer.Default))
                        {
                            formatExpression = argument.Value;
                        }
                        else if (parameter.Equals(paramsArgument, SymbolEqualityComparer.Default))
                        {
                            var parameterType = argument.Parameter.Type;// TODO test
                            if (parameterType == null)
                            {
                                return;
                            }

                            //Detect if current argument can be passed directly to args
                            argsIsArray = parameterType.TypeKind == TypeKind.Array && ((IArrayTypeSymbol)parameterType).ElementType.SpecialType == SpecialType.System_Object;

                            paramsCount++;
                        }
                    }
                }

                if (formatExpression is not null)
                {
                    AnalyzeFormatArgument(context, formatExpression, paramsCount, argsIsArray);
                }
            }
        }

        private void AnalyzeFormatArgument(OperationAnalysisContext context, IOperation formatExpression, int paramsCount, bool argsIsArray)
        {
            var text = TryGetFormatText(formatExpression);
            if (text == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(CA2254Rule, formatExpression.Syntax.GetLocation()));
                return;
            }

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

            foreach (var valueName in formatter.ValueNames)
            {
                if (int.TryParse(valueName, out _))
                {
                    context.ReportDiagnostic(Diagnostic.Create(CA2253Rule, formatExpression.Syntax.GetLocation()));
                }
                else if (char.IsLower(valueName[0]))
                {
                    context.ReportDiagnostic(Diagnostic.Create(CA1727Rule, formatExpression.Syntax.GetLocation()));
                }
            }

            var argsPassedDirectly = argsIsArray && paramsCount == 1;
            if (!argsPassedDirectly && paramsCount != formatter.ValueNames.Count)
            {
                context.ReportDiagnostic(Diagnostic.Create(CA2255Rule, formatExpression.Syntax.GetLocation()));
            }
        }

        private string? TryGetFormatText(IOperation? argumentExpression)
        {
            if (argumentExpression is null)
                return null;

            switch (argumentExpression)
            {
                case ILiteralOperation { ConstantValue: { HasValue: true, Value: string constantValue } }:
                    return constantValue;
                case IInterpolatedStringOperation interpolated:
                    var text = "";
                    foreach (var interpolatedStringContent in interpolated.Parts)
                    {
                        if (interpolatedStringContent is IInterpolatedStringTextOperation textSyntax)
                        {
                            text += textSyntax.Text;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    return text;
                case INameOfOperation:
                    // return placeholder from here because actual value is not required for analysis and is hard to get
                    return "NAMEOF";
                case IParenthesizedOperation parenthesized:
                    return TryGetFormatText(parenthesized.Operand);
                case IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } binary:
                    var leftText = TryGetFormatText(binary.LeftOperand);
                    var rightText = TryGetFormatText(binary.RightOperand);

                    if (leftText != null && rightText != null)
                    {
                        return leftText + rightText;
                    }

                    return null;
                default:
                    var constant = argumentExpression.ConstantValue;
                    if (constant.HasValue && constant.Value is string constantString)
                    {
                        return constantString;
                    }
                    return null;
            }
        }

        private static bool FindLogParameters(IMethodSymbol methodSymbol, out IParameterSymbol? message, out IParameterSymbol? arguments)
        {
            message = null;
            arguments = null;
            foreach (var parameter in methodSymbol.Parameters)
            {
                if (parameter.Type.SpecialType == SpecialType.System_String &&
                    string.Equals(parameter.Name, "message", StringComparison.Ordinal) ||
                    string.Equals(parameter.Name, "messageFormat", StringComparison.Ordinal) ||
                    string.Equals(parameter.Name, "formatString", StringComparison.Ordinal))
                {
                    message = parameter;
                }

                // When calling logger.BeginScope("{Param}") generic overload would be selected
                if (parameter.Type.SpecialType == SpecialType.System_String &&
                    methodSymbol.Name.Equals("BeginScope") &&
                    string.Equals(parameter.Name, "state", StringComparison.Ordinal))
                {
                    message = parameter;
                }

                if (parameter.IsParams &&
                    string.Equals(parameter.Name, "args", StringComparison.Ordinal))
                {
                    arguments = parameter;
                }
            }
            return message != null;
        }
    }
}