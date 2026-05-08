// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2208: <inheritdoc cref="InstantiateArgumentExceptionsCorrectlyTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class InstantiateArgumentExceptionsCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2208";
        internal const string MessagePosition = nameof(MessagePosition);

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(InstantiateArgumentExceptionsCorrectlyTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(InstantiateArgumentExceptionsCorrectlyDescription));

        internal static readonly DiagnosticDescriptor RuleNoArguments = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(InstantiateArgumentExceptionsCorrectlyMessageNoArguments)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleIncorrectMessage = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(InstantiateArgumentExceptionsCorrectlyMessageIncorrectMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleIncorrectParameterName = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(InstantiateArgumentExceptionsCorrectlyMessageIncorrectParameterName)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(RuleNoArguments, RuleIncorrectMessage, RuleIncorrectParameterName);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                compilationContext =>
                {
                    Compilation compilation = compilationContext.Compilation;
                    ITypeSymbol? argumentExceptionType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemArgumentException);

                    if (argumentExceptionType == null)
                    {
                        return;
                    }

                    compilationContext.RegisterOperationAction(
                        operationContext => AnalyzeObjectCreation(
                            operationContext,
                            operationContext.ContainingSymbol,
                            argumentExceptionType),
                        OperationKind.ObjectCreation);
                });
        }

        private static void AnalyzeObjectCreation(
            OperationAnalysisContext context,
            ISymbol owningSymbol,
            ITypeSymbol argumentExceptionType)
        {
            var creation = (IObjectCreationOperation)context.Operation;
            if (!creation.Type.Inherits(argumentExceptionType) || !MatchesConfiguredVisibility(owningSymbol, context) || !HasParameterNameConstructor(creation.Type))
            {
                return;
            }

            if (creation.Arguments.IsEmpty)
            {
                if (HasParameters(owningSymbol))
                {
                    // Call the {0} constructor that contains a message and/ or paramName parameter
                    context.ReportDiagnostic(context.Operation.Syntax.CreateDiagnostic(RuleNoArguments, creation.Type.Name));
                }
            }
            else
            {
                Diagnostic? diagnosticFound = null;
                foreach (IArgumentOperation argument in creation.Arguments)
                {
                    if (argument.Parameter?.Type.SpecialType != SpecialType.System_String)
                    {
                        continue;
                    }

                    string? value = argument.Value.ConstantValue.HasValue ? argument.Value.ConstantValue.Value as string : null;
                    if (value == null)
                    {
                        continue;
                    }

                    Diagnostic? diagnostic = CheckArgument(owningSymbol, creation, argument.Parameter, value, context);

                    if (diagnostic != null)
                    {
                        diagnosticFound = diagnostic;
                        // RuleIncorrectMessage is the highest priority rule, no need to check other rules
                        if (diagnostic.Descriptor.Equals(RuleIncorrectMessage))
                        {
                            break;
                        }
                    }
                }

                if (diagnosticFound != null)
                {
                    context.ReportDiagnostic(diagnosticFound);
                }
            }
        }

        private static bool MatchesConfiguredVisibility(ISymbol owningSymbol, OperationAnalysisContext context) =>
             context.Options.MatchesConfiguredVisibility(RuleIncorrectParameterName, owningSymbol, context.Compilation,
                 defaultRequiredVisibility: SymbolVisibilityGroup.All);

        private static bool HasParameters(ISymbol owningSymbol) => !owningSymbol.GetParameters().IsEmpty;

        private static Diagnostic? CheckArgument(
            ISymbol targetSymbol,
            IObjectCreationOperation creation,
            IParameterSymbol parameter,
            string stringArgument,
            OperationAnalysisContext context)
        {
            if (IsMessage(parameter) && MatchesParameterStrict(targetSymbol, creation, stringArgument))
            {
                var dictBuilder = ImmutableDictionary.CreateBuilder<string, string?>();
                dictBuilder.Add(MessagePosition, parameter.Ordinal.ToString(CultureInfo.InvariantCulture));
                return context.Operation.CreateDiagnostic(RuleIncorrectMessage, dictBuilder.ToImmutable(), targetSymbol.Name, stringArgument, parameter.Name, creation.Type!.Name);
            }

            if (HasParameters(targetSymbol) && IsParameterName(parameter) && !MatchesParameterRelax(targetSymbol, creation, stringArgument))
            {
                // Allow argument exceptions in accessors to use the associated property symbol name.
                if (!MatchesAssociatedSymbol(targetSymbol, stringArgument))
                {
                    return context.Operation.CreateDiagnostic(RuleIncorrectParameterName, targetSymbol.Name, stringArgument, parameter.Name, creation.Type!.Name);
                }
            }

            return null;
        }

        private static bool IsMessage(IParameterSymbol parameter)
        {
            return parameter.Name == "message";
        }

        private static bool IsParameterName(IParameterSymbol parameter)
        {
            return parameter.Name is "paramName" or "parameterName";
        }

        private static bool HasParameterNameConstructor(ITypeSymbol type)
        {
            foreach (ISymbol member in type.GetMembers())
            {
                if (!member.IsConstructor())
                {
                    continue;
                }

                foreach (IParameterSymbol parameter in member.GetParameters())
                {
                    if (parameter.Type.SpecialType == SpecialType.System_String
                        && IsParameterName(parameter))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchesParameterStrict(ISymbol? symbol, IObjectCreationOperation creation, string stringArgumentValue)
        {
            return MatchesParameterCore(symbol, creation, stringArgumentValue, strict: true);
        }

        private static bool MatchesParameterRelax(ISymbol? symbol, IObjectCreationOperation creation, string stringArgumentValue)
        {
            return MatchesParameterCore(symbol, creation, stringArgumentValue, strict: false);
        }

        private static bool MatchesParameterCore(ISymbol? symbol, IObjectCreationOperation creation, string stringArgumentValue, bool strict)
        {
            if (MatchesParameterCore(symbol, stringArgumentValue, strict))
            {
                return true;
            }

            var operation = creation.Parent;
            while (operation != null)
            {
                symbol = null;
                switch (operation.Kind)
                {
                    case OperationKind.LocalFunction:
                        symbol = ((ILocalFunctionOperation)operation).Symbol;
                        break;

                    case OperationKind.AnonymousFunction:
                        symbol = ((IAnonymousFunctionOperation)operation).Symbol;
                        break;
                }

                if (symbol != null && MatchesParameterCore(symbol, stringArgumentValue, strict))
                {
                    return true;
                }

                operation = operation.Parent;
            }

            return false;
        }

        private static bool MatchesParameterCore(ISymbol? symbol, string stringArgumentValue, bool strict)
        {
            foreach (IParameterSymbol parameter in symbol.GetParameters())
            {
                // If the parameter name matches exactly, it's a match.
                if (parameter.Name == stringArgumentValue)
                {
                    return true;
                }

                if (!strict)
                {
                    // If the string argument begins with the parameter name followed by punctuation, it's also considered a match.
                    // e.g. "arg.Length", "arg[0]", etc.
                    if (stringArgumentValue.Length > parameter.Name.Length &&
                        stringArgumentValue.StartsWith(parameter.Name, StringComparison.Ordinal) &&
                        char.IsPunctuation(stringArgumentValue, parameter.Name.Length))
                    {
                        return true;
                    }
                }
            }

            if (symbol is IMethodSymbol method)
            {
                if (method.IsGenericMethod)
                {
                    foreach (ITypeParameterSymbol parameter in method.TypeParameters)
                    {
                        if (parameter.Name == stringArgumentValue)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool MatchesAssociatedSymbol(ISymbol targetSymbol, string stringArgument)
            => targetSymbol.IsAccessorMethod() &&
            ((IMethodSymbol)targetSymbol).AssociatedSymbol?.Name == stringArgument;
    }
}