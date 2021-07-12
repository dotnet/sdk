// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2241: Provide correct arguments to formatting methods
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class ProvideCorrectArgumentsToFormattingMethodsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2241";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentsToFormattingMethodsTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentsToFormattingMethodsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ProvideCorrectArgumentsToFormattingMethodsDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.BuildWarningCandidate,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var formatInfo = new StringFormatInfo(compilationContext.Compilation);

                compilationContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;

                    StringFormatInfo.Info? info = formatInfo.TryGet(invocation.TargetMethod, operationContext);
                    if (info == null || invocation.Arguments.Length <= info.FormatStringIndex)
                    {
                        // not a target method
                        return;
                    }

                    IArgumentOperation formatStringArgument = invocation.Arguments[info.FormatStringIndex];
                    if (!object.Equals(formatStringArgument?.Value?.Type, formatInfo.String) ||
                        !(formatStringArgument?.Value?.ConstantValue.Value is string))
                    {
                        // wrong argument
                        return;
                    }

                    var stringFormat = (string)formatStringArgument.Value.ConstantValue.Value;
                    int expectedStringFormatArgumentCount = GetFormattingArguments(stringFormat);

                    // explicit parameter case
                    if (info.ExpectedStringFormatArgumentCount >= 0)
                    {
                        // __arglist is not supported here
                        if (invocation.TargetMethod.IsVararg)
                        {
                            // can't deal with this for now.
                            return;
                        }

                        if (info.ExpectedStringFormatArgumentCount != expectedStringFormatArgumentCount)
                        {
                            operationContext.ReportDiagnostic(operationContext.Operation.Syntax.CreateDiagnostic(Rule));
                        }

                        return;
                    }

                    // ensure argument is an array
                    IArgumentOperation paramsArgument = invocation.Arguments[info.FormatStringIndex + 1];
                    if (paramsArgument.ArgumentKind is not ArgumentKind.ParamArray and not ArgumentKind.Explicit)
                    {
                        // wrong format
                        return;
                    }

                    if (paramsArgument.Value is not IArrayCreationOperation arrayCreation ||
                        arrayCreation.GetElementType() is not ITypeSymbol elementType ||
                        !object.Equals(elementType, formatInfo.Object) ||
                        arrayCreation.DimensionSizes.Length != 1)
                    {
                        // wrong format
                        return;
                    }

                    // compiler generating object array for params case
                    IArrayInitializerOperation intializer = arrayCreation.Initializer;
                    if (intializer == null)
                    {
                        // unsupported format
                        return;
                    }

                    // REVIEW: "ElementValues" is a bit confusing where I need to double dot those to get number of elements
                    int actualArgumentCount = intializer.ElementValues.Length;
                    if (actualArgumentCount != expectedStringFormatArgumentCount)
                    {
                        operationContext.ReportDiagnostic(operationContext.Operation.Syntax.CreateDiagnostic(Rule));
                    }
                }, OperationKind.Invocation);
            });
        }

        private static int GetFormattingArguments(string format)
        {
            // code is from mscorlib
            // https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/Text/StringBuilder.cs#L1312

            // return count of this format - {index[,alignment][:formatString]}
            var pos = 0;
            int len = format.Length;
            var uniqueNumbers = new HashSet<int>();

            // main loop
            while (true)
            {
                // loop to find starting "{"
                char ch;
                while (pos < len)
                {
                    ch = format[pos];

                    pos++;
                    if (ch == '}')
                    {
                        if (pos < len && format[pos] == '}') // Treat as escape character for }}
                        {
                            pos++;
                        }
                        else
                        {
                            return -1;
                        }
                    }

                    if (ch == '{')
                    {
                        if (pos < len && format[pos] == '{') // Treat as escape character for {{
                        {
                            pos++;
                        }
                        else
                        {
                            pos--;
                            break;
                        }
                    }
                }

                // finished with "{"
                if (pos == len)
                {
                    break;
                }

                pos++;

                if (pos == len || (ch = format[pos]) < '0' || ch > '9')
                {
                    // finished with "{x"
                    return -1;
                }

                // searching for index
                var index = 0;
                do
                {
                    index = index * 10 + ch - '0';

                    pos++;
                    if (pos == len)
                    {
                        // wrong index format
                        return -1;
                    }

                    ch = format[pos];
                } while (ch >= '0' && ch <= '9' && index < 1000000);

                // eat up whitespace
                while (pos < len && (ch = format[pos]) == ' ')
                {
                    pos++;
                }

                // searching for alignment
                var width = 0;
                if (ch == ',')
                {
                    pos++;

                    // eat up whitespace
                    while (pos < len && format[pos] == ' ')
                    {
                        pos++;
                    }

                    if (pos == len)
                    {
                        // wrong format, reached end without "}"
                        return -1;
                    }

                    ch = format[pos];
                    if (ch == '-')
                    {
                        pos++;

                        if (pos == len)
                        {
                            // wrong format. reached end without "}"
                            return -1;
                        }

                        ch = format[pos];
                    }

                    if (ch is < '0' or > '9')
                    {
                        // wrong format after "-"
                        return -1;
                    }

                    do
                    {
                        width = width * 10 + ch - '0';
                        pos++;

                        if (pos == len)
                        {
                            // wrong width format
                            return -1;
                        }

                        ch = format[pos];
                    } while (ch >= '0' && ch <= '9' && width < 1000000);
                }

                // eat up whitespace
                while (pos < len && (ch = format[pos]) == ' ')
                {
                    pos++;
                }

                // searching for embedded format string
                if (ch == ':')
                {
                    pos++;

                    while (true)
                    {
                        if (pos == len)
                        {
                            // reached end without "}"
                            return -1;
                        }

                        ch = format[pos];
                        pos++;

                        if (ch == '{')
                        {
                            if (pos < len && format[pos] == '{')  // Treat as escape character for {{
                                pos++;
                            else
                                return -1;
                        }
                        else if (ch == '}')
                        {
                            if (pos < len && format[pos] == '}')  // Treat as escape character for }}
                            {
                                pos++;
                            }
                            else
                            {
                                pos--;
                                break;
                            }
                        }
                    }
                }

                if (ch != '}')
                {
                    // "}" is expected
                    return -1;
                }

                pos++;

                uniqueNumbers.Add(index);

            } // end of main loop

            return uniqueNumbers.Count;
        }

        private class StringFormatInfo
        {
            private const string Format = "format";

            private readonly ImmutableDictionary<IMethodSymbol, Info> _map;

            public StringFormatInfo(Compilation compilation)
            {
                ImmutableDictionary<IMethodSymbol, Info>.Builder builder = ImmutableDictionary.CreateBuilder<IMethodSymbol, Info>();

                INamedTypeSymbol? console = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConsole);
                AddStringFormatMap(builder, console, "Write");
                AddStringFormatMap(builder, console, "WriteLine");

                INamedTypeSymbol @string = compilation.GetSpecialType(SpecialType.System_String);
                AddStringFormatMap(builder, @string, "Format");

                _map = builder.ToImmutable();

                String = @string;
                Object = compilation.GetSpecialType(SpecialType.System_Object);
            }

            public INamedTypeSymbol String { get; }
            public INamedTypeSymbol Object { get; }

            public Info? TryGet(IMethodSymbol method, OperationAnalysisContext context)
            {
                if (_map.TryGetValue(method, out Info? info))
                {
                    return info;
                }

                // Check if this the underlying method is user configured string formatting method.
                var additionalStringFormatMethodsOption = context.Options.GetAdditionalStringFormattingMethodsOption(Rule, context.Operation.Syntax.SyntaxTree, context.Compilation);
                if (additionalStringFormatMethodsOption.Contains(method.OriginalDefinition) &&
                    TryGetFormatInfo(method, out info))
                {
                    return info;
                }

                // Check if the user configured automatic determination of formatting methods.
                // If so, check if the method called has a 'string format' parameter followed by an params array.
                var determineAdditionalStringFormattingMethodsAutomatically = context.Options.GetBoolOptionValue(EditorConfigOptionNames.TryDetermineAdditionalStringFormattingMethodsAutomatically,
                        Rule, context.Operation.Syntax.SyntaxTree, context.Compilation, defaultValue: false);
                if (determineAdditionalStringFormattingMethodsAutomatically &&
                    TryGetFormatInfo(method, out info) &&
                    info.ExpectedStringFormatArgumentCount == -1)
                {
                    return info;
                }

                return null;
            }

            private static void AddStringFormatMap(ImmutableDictionary<IMethodSymbol, Info>.Builder builder, INamedTypeSymbol? type, string methodName)
            {
                if (type == null)
                {
                    return;
                }

                foreach (IMethodSymbol method in type.GetMembers(methodName).OfType<IMethodSymbol>())
                {
                    if (TryGetFormatInfo(method, out var formatInfo))
                    {
                        builder.Add(method, formatInfo);
                    }
                }
            }

            private static bool TryGetFormatInfo(IMethodSymbol method, [NotNullWhen(returnValue: true)] out Info? formatInfo)
            {
                formatInfo = default;

                int formatIndex = FindParameterIndexOfName(method.Parameters, Format);
                if (formatIndex < 0 || formatIndex == method.Parameters.Length - 1)
                {
                    // no valid format string
                    return false;
                }

                if (method.Parameters[formatIndex].Type.SpecialType != SpecialType.System_String)
                {
                    // no valid format string
                    return false;
                }

                int expectedArguments = GetExpectedNumberOfArguments(method.Parameters, formatIndex);
                formatInfo = new Info(formatIndex, expectedArguments);
                return true;
            }

            private static int GetExpectedNumberOfArguments(ImmutableArray<IParameterSymbol> parameters, int formatIndex)
            {
                // check params
                IParameterSymbol nextParameter = parameters[formatIndex + 1];
                if (nextParameter.IsParams)
                {
                    return -1;
                }

                return parameters.Length - formatIndex - 1;
            }

            private static int FindParameterIndexOfName(ImmutableArray<IParameterSymbol> parameters, string name)
            {
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (string.Equals(parameters[i].Name, name, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }

                return -1;
            }

            public class Info
            {
                public Info(int formatIndex, int expectedArguments)
                {
                    FormatStringIndex = formatIndex;
                    ExpectedStringFormatArgumentCount = expectedArguments;
                }

                public int FormatStringIndex { get; }
                public int ExpectedStringFormatArgumentCount { get; }
            }
        }
    }
}