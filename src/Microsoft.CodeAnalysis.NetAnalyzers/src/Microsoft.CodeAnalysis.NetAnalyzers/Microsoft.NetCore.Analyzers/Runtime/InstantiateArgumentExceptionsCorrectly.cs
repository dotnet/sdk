// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2208: Instantiate argument exceptions correctly
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class InstantiateArgumentExceptionsCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2208";
        private const string HelpUri = "https://docs.microsoft.com/visualstudio/code-quality/ca2208-instantiate-argument-exceptions-correctly";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageNoArguments = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageNoArguments), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageIncorrectMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageIncorrectMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageIncorrectParameterName = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageIncorrectParameterName), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             "{0}",
                                                                             DiagnosticCategory.Usage,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: HelpUri,
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(
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
            if (!creation.Type.Inherits(argumentExceptionType))
            {
                return;
            }

            if (creation.Arguments.Length == 0)
            {
                if (HasMessageOrParameterNameConstructor(creation.Type))
                {
                    // Call the {0} constructor that contains a message and/ or paramName parameter
                    ReportDiagnostic(context, s_localizableMessageNoArguments, creation.Type.Name);
                }
            }
            else
            {
                foreach (IArgumentOperation argument in creation.Arguments)
                {
                    if (argument.Parameter.Type.SpecialType != SpecialType.System_String)
                    {
                        continue;
                    }

                    string? value = argument.Value.ConstantValue.HasValue ? argument.Value.ConstantValue.Value as string : null;
                    if (value == null)
                    {
                        continue;
                    }

                    CheckArgument(owningSymbol, creation, argument.Parameter, value, context);
                }
            }
        }
        private static void CheckArgument(
            ISymbol targetSymbol,
            IObjectCreationOperation creation,
            IParameterSymbol parameter,
            string stringArgument,
            OperationAnalysisContext context)
        {
            bool matchesParameter = MatchesParameter(targetSymbol, creation, stringArgument);
            LocalizableString? format = null;

            if (IsMessage(parameter) && matchesParameter)
            {
                format = s_localizableMessageIncorrectMessage;
            }
            else if (IsParameterName(parameter) && !matchesParameter)
            {
                // Allow argument exceptions in accessors to use the associated property symbol name.
                if (MatchesAssociatedSymbol(targetSymbol, stringArgument))
                {
                    return;
                }

                format = s_localizableMessageIncorrectParameterName;
            }

            if (format != null)
            {
                ReportDiagnostic(context, format, targetSymbol.Name, stringArgument, parameter.Name, creation.Type.Name);
            }
        }

        private static void ReportDiagnostic(OperationAnalysisContext context, LocalizableString format, params object[] args)
        {
            context.ReportDiagnostic(
                context.Operation.Syntax.CreateDiagnostic(
                    Descriptor,
                    string.Format(format.ToString(), args)));
        }

        private static bool IsMessage(IParameterSymbol parameter)
        {
            return parameter.Name == "message";
        }

        private static bool IsParameterName(IParameterSymbol parameter)
        {
            return parameter.Name == "paramName" || parameter.Name == "parameterName";
        }

        private static bool HasMessageOrParameterNameConstructor(ITypeSymbol type)
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
                        && (IsMessage(parameter) || IsParameterName(parameter)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchesParameter(ISymbol? symbol, IObjectCreationOperation creation, string stringArgumentValue)
        {
            if (MatchesParameterCore(symbol, stringArgumentValue))
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

                if (symbol != null && MatchesParameterCore(symbol, stringArgumentValue))
                {
                    return true;
                }

                operation = operation.Parent;
            }

            return false;
        }

        private static bool MatchesParameterCore(ISymbol? symbol, string stringArgumentValue)
        {
            foreach (IParameterSymbol parameter in symbol.GetParameters())
            {
                if (parameter.Name == stringArgumentValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAssociatedSymbol(ISymbol targetSymbol, string stringArgument)
            => targetSymbol.IsAccessorMethod() &&
            ((IMethodSymbol)targetSymbol).AssociatedSymbol?.Name == stringArgument;
    }
}