// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2241: <inheritdoc cref="ProvideCorrectArgumentsToFormattingMethodsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class ProvideCorrectArgumentsToFormattingMethodsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2241";

        internal static readonly DiagnosticDescriptor ArgumentCountRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ProvideCorrectArgumentsToFormattingMethodsTitle)),
            CreateLocalizableResourceString(nameof(ProvideCorrectArgumentsToFormattingMethodsMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarningCandidate,
            description: CreateLocalizableResourceString(nameof(ProvideCorrectArgumentsToFormattingMethodsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor InvalidFormatRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ProvideCorrectArgumentsToFormattingMethodsTitle)),
            CreateLocalizableResourceString(nameof(ProvideCorrectArgumentsToFormattingMethodsInvalidFormatMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarningCandidate,
            description: CreateLocalizableResourceString(nameof(ProvideCorrectArgumentsToFormattingMethodsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(ArgumentCountRule, InvalidFormatRule);

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
                    if (!Equals(formatStringArgument.Value.Type, formatInfo.String) ||
                        !(formatStringArgument.Value.ConstantValue.Value is string stringFormat))
                    {
                        // wrong argument or not a constant
                        return;
                    }

                    int expectedStringFormatArgumentCount = GetFormattingArguments(stringFormat);

                    if (expectedStringFormatArgumentCount < 0)
                    {
                        // Malformed format specification
                        operationContext.ReportDiagnostic(operationContext.Operation.Syntax.CreateDiagnostic(InvalidFormatRule));
                        return;
                    }

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
                            operationContext.ReportDiagnostic(operationContext.Operation.Syntax.CreateDiagnostic(ArgumentCountRule));
                        }

                        return;
                    }
                    else if (info.ExpectedStringFormatArgumentCount == -2)
                    {
                        // Not a formatting method, we only checked the format specification.
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
                        !Equals(elementType, formatInfo.Object) ||
                        arrayCreation.DimensionSizes.Length != 1)
                    {
                        // wrong format
                        return;
                    }

                    // compiler generating object array for params case
                    if (arrayCreation.Initializer is not { } initializer)
                    {
                        // unsupported format
                        return;
                    }

                    // REVIEW: "ElementValues" is a bit confusing where I need to double dot those to get number of elements
                    int actualArgumentCount = initializer.ElementValues.Length;
                    if (actualArgumentCount != expectedStringFormatArgumentCount)
                    {
                        operationContext.ReportDiagnostic(operationContext.Operation.Syntax.CreateDiagnostic(ArgumentCountRule));
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

            return uniqueNumbers.Count == 0 ? 0 : uniqueNumbers.Max() + 1;
        }

        private class StringFormatInfo
        {
            private const string Format = "format";
            private const string CompositeFormat = "CompositeFormat";

            private readonly ConcurrentDictionary<IMethodSymbol, Info?> _map;

            public StringFormatInfo(Compilation compilation)
            {
                _map = new ConcurrentDictionary<IMethodSymbol, Info?>();

                INamedTypeSymbol? console = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemConsole);
                AddStringFormatMap(console, "Write");
                AddStringFormatMap(console, "WriteLine");

                INamedTypeSymbol @string = compilation.GetSpecialType(SpecialType.System_String);
                AddStringFormatMap(@string, "Format");

                String = @string;
                Object = compilation.GetSpecialType(SpecialType.System_Object);
                StringSyntaxAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsCodeAnalysisStringSyntaxAttributeName);
            }

            public INamedTypeSymbol String { get; }
            public INamedTypeSymbol Object { get; }
            public INamedTypeSymbol? StringSyntaxAttribute { get; }

            public Info? TryGet(IMethodSymbol method, OperationAnalysisContext context)
            {
                if (_map.TryGetValue(method, out Info? info))
                {
                    return info;
                }

                if (StringSyntaxAttribute is not null &&
                    TryGetFormatInfoByCompositeFormatStringSyntaxAttribute(method, out info))
                {
                    return info;
                }

                // Check if this the underlying method is user configured string formatting method.
                var additionalStringFormatMethodsOption = context.Options.GetAdditionalStringFormattingMethodsOption(ArgumentCountRule, context.Operation.Syntax.SyntaxTree, context.Compilation);
                if (additionalStringFormatMethodsOption.Contains(method.OriginalDefinition) &&
                    TryGetFormatInfoByParameterName(method, out info))
                {
                    return info;
                }

                // Check if the user configured automatic determination of formatting methods.
                // If so, check if the method called has a 'string format' parameter followed by an params array.
                var determineAdditionalStringFormattingMethodsAutomatically = context.Options.GetBoolOptionValue(EditorConfigOptionNames.TryDetermineAdditionalStringFormattingMethodsAutomatically,
                        ArgumentCountRule, context.Operation.Syntax.SyntaxTree, context.Compilation, defaultValue: false);
                if (determineAdditionalStringFormattingMethodsAutomatically &&
                    TryGetFormatInfoByParameterName(method, out info))
                {
                    if (info.ExpectedStringFormatArgumentCount == -1)
                    {
                        return info;
                    }
                    else
                    {
                        // TryGetFormatInfoByParameterName always adds info to _map
                        // We only support heuristically found format method that use 'params'.
                        _map.TryRemove(method, out _);
                    }
                }

                _map.TryAdd(method, null);
                return null;
            }

            private void AddStringFormatMap(INamedTypeSymbol? type, string methodName)
            {
                if (type == null)
                {
                    return;
                }

                foreach (IMethodSymbol method in type.GetMembers(methodName).OfType<IMethodSymbol>())
                {
                    TryGetFormatInfoByParameterName(method, out var _);
                }
            }

            private bool TryGetFormatInfoByCompositeFormatStringSyntaxAttribute(IMethodSymbol method, [NotNullWhen(returnValue: true)] out Info? formatInfo)
            {
                int formatIndex = FindParameterIndexOfCompositeFormatStringSyntaxAttribute(method.Parameters);
                return TryGetFormatInfo(method, formatIndex, out formatInfo);
            }

            private bool TryGetFormatInfoByParameterName(IMethodSymbol method, [NotNullWhen(returnValue: true)] out Info? formatInfo)
            {
                int formatIndex = FindParameterIndexByParameterName(method.Parameters, Format);
                return TryGetFormatInfo(method, formatIndex, out formatInfo);
            }

            private bool TryGetFormatInfo(IMethodSymbol method, int formatIndex, [NotNullWhen(returnValue: true)] out Info? formatInfo)
            {
                formatInfo = default;

                if (formatIndex < 0)
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
                _map.TryAdd(method, formatInfo);

                return true;
            }

            private static int GetExpectedNumberOfArguments(ImmutableArray<IParameterSymbol> parameters, int formatIndex)
            {
                if (formatIndex == parameters.Length - 1)
                {
                    // format specification is the last parameter (e.g. CompositeFormat.Parse)
                    // this is therefore not a formatting method.
                    return -2;
                }

                // check params
                IParameterSymbol nextParameter = parameters[formatIndex + 1];
                if (nextParameter.IsParams)
                {
                    return -1;
                }

                return parameters.Length - formatIndex - 1;
            }

            private static int FindParameterIndexByParameterName(ImmutableArray<IParameterSymbol> parameters, string name)
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

            private int FindParameterIndexOfCompositeFormatStringSyntaxAttribute(ImmutableArray<IParameterSymbol> parameters)
            {
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (String.Equals(parameters[i].Type, SymbolEqualityComparer.Default))
                    {
                        foreach (AttributeData attribute in parameters[i].GetAttributes())
                        {
                            if (StringSyntaxAttribute!.Equals(attribute.AttributeClass, SymbolEqualityComparer.Default))
                            {
                                ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;
                                if (arguments.Length == 1 && CompositeFormat.Equals(arguments[0].Value))
                                {
                                    return i;
                                }
                            }
                        }
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
