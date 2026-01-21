// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    public sealed partial class CSharpUseCompiledLogMessagesFixer
    {
        /// <summary>
        /// Tracks a bunch of metadata about a potential fix to apply.
        /// </summary>
        internal sealed class FixDetails
        {
            public int MessageParamIndex { get; }
            public int ExceptionParamIndex { get; }
            public int EventIdParamIndex { get; }
            public int LogLevelParamIndex { get; }
            public int ArgsParamIndex { get; }
            public string Message { get; } = string.Empty;
            public string Level { get; } = string.Empty;
            public string TargetFilename { get; }
            public string TargetNamespace { get; }
            public string TargetClassName { get; }
            public string TargetMethodName { get; }
            public IReadOnlyList<string> MessageArgs { get; }
            public IReadOnlyList<IOperation>? InterpolationArgs { get; }

            public FixDetails(
                IMethodSymbol method,
                IInvocationOperation invocationOp,
                string? defaultNamespace,
                IEnumerable<Document> docs)
            {
                (MessageParamIndex, ExceptionParamIndex, EventIdParamIndex, LogLevelParamIndex, ArgsParamIndex) = IdentifyParameters(method);

                if (MessageParamIndex >= 0)
                {
                    var op = invocationOp.Arguments[MessageParamIndex];
                    var children = op.Children.ToArray();
                    if (children.Length == 1)
                    {
                        if (children[0].ConstantValue.HasValue)
                        {
                            Message = children[0].ConstantValue.Value as string ?? string.Empty;
                        }
                        else if (children[0] is IInterpolatedStringOperation)
                        {
                            var interpolationArgs = new List<IOperation>();
                            var messageArgs = new List<string>();
                            InterpolationArgs = interpolationArgs;
                            MessageArgs = messageArgs;

                            var sb = new StringBuilder();
                            int argCount = 0;
                            foreach (var o in children[0].Children)
                            {
                                switch (o)
                                {
                                    case IInterpolatedStringTextOperation stringOp:
                                        _ = sb.Append(stringOp.Children.First().ConstantValue.Value as string);
                                        break;

                                    case IInterpolationOperation intOp:
                                        var operation = intOp.Children.First();
                                        var argName = operation.Syntax.GetLastToken().Text;

                                        if (argName.Length == 0)
                                        {
                                            argName = $"_arg{argCount++}";
                                        }
                                        else
                                        {
                                            char firstChar = argName[0];
                                            if (firstChar >= 'A' && firstChar <= 'Z')
                                            {
                                                argName = (char)(firstChar + ('a' - 'A')) + argName.Substring(1);
                                            }
                                            else if (firstChar < 'a' || firstChar > 'z')
                                            {
                                                argName = $"_arg{argCount++}";
                                            }
                                        }

                                        _ = sb.Append("{" + argName + "}");
                                        messageArgs.Add(argName);
                                        interpolationArgs.Add(intOp.Children.First());
                                        break;
                                }
                            }

                            Message = sb.ToString();
                        }
                    }
                }

                if (LogLevelParamIndex > 0)
                {
                    object? value = null;

                    var op = invocationOp.Arguments[LogLevelParamIndex].Descendants().SingleOrDefault(x => x.Kind == OperationKind.Literal || x.Kind == OperationKind.FieldReference);
                    switch (op)
                    {
                        case ILiteralOperation lit:
                            value = lit.ConstantValue.Value;
                            break;

                        case IFieldReferenceOperation fieldRef:
                            value = fieldRef.ConstantValue.Value;
                            break;
                    }

                    if (value is int)
                    {
                        Level = GetLogLevelName((LogLevel)value);
                    }
                }
                else
                {
                    Level = method.Name.Substring("Log".Length);
                }

                TargetFilename = FindUniqueFilename(docs);
                TargetNamespace = defaultNamespace ?? string.Empty;
                TargetClassName = "Log";
                TargetMethodName = DeriveName(Message);
                MessageArgs ??= ExtractTemplateArgs(Message);
            }

            public string FullTargetClassName
            {
                get
                {
                    if (string.IsNullOrEmpty(TargetNamespace))
                    {
                        return TargetClassName;
                    }

                    return $"{TargetNamespace}.{TargetClassName}";
                }
            }

            internal static string GetLogLevelName(LogLevel value)
            {
                return value switch
                {
                    LogLevel.Trace => "Trace",
                    LogLevel.Debug => "Debug",
                    LogLevel.Information => "Information",
                    LogLevel.Warning => "Warning",
                    LogLevel.Error => "Error",
                    LogLevel.Critical => "Critical",
                    _ => string.Empty,
                };
            }

            private static string FindUniqueFilename(IEnumerable<Document> docs)
            {
                var targetName = "Log.cs";
                int count = 2;
                bool duplicate;
                do
                {
                    duplicate = false;
                    foreach (var doc in docs)
                    {
                        if (string.Equals(doc.Name, targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            duplicate = true;
                            targetName = $"Log{count}.cs";
                            count++;
                            break;
                        }
                    }
                }
                while (duplicate);

                return targetName;
            }

            /// <summary>
            /// Finds the position of the well-known parameters of legacy logging methods.
            /// </summary>
            /// <returns>-1 for any parameter not present in the given overload.</returns>
            private static (int message, int exception, int eventId, int logLevel, int args) IdentifyParameters(IMethodSymbol method)
            {
                var message = -1;
                var exception = -1;
                var eventId = -1;
                var logLevel = -1;
                var args = -1;

                int index = 0;
                foreach (var p in method.Parameters)
                {
                    switch (p.Name)
                    {
                        case "message":
                            message = index;
                            break;
                        case "exception":
                            exception = index;
                            break;
                        case "eventId":
                            eventId = index;
                            break;
                        case "logLevel":
                            logLevel = index;
                            break;
                        case "args":
                            args = index;
                            break;
                    }

                    index++;
                }

                return (message, exception, eventId, logLevel, args);
            }

            /// <summary>
            /// Given a logging message with template args, generate a reasonable logging method name.
            /// </summary>
            private static string DeriveName(string message)
            {
                var sb = new StringBuilder();
                bool capitalizeNext = true;
                foreach (var ch in message)
                {
                    if (char.IsLetter(ch) || (char.IsLetterOrDigit(ch) && sb.Length > 1))
                    {
                        if (capitalizeNext)
                        {
                            _ = sb.Append(char.ToUpperInvariant(ch));
                            capitalizeNext = false;
                        }
                        else
                        {
                            _ = sb.Append(ch);
                        }
                    }
                    else
                    {
                        capitalizeNext = true;
                    }
                }

                return sb.ToString();
            }

            private static readonly char[] _formatDelimiters = { ',', ':' };

            /// <summary>
            /// Finds the template arguments contained in the message string.
            /// </summary>
            private static List<string> ExtractTemplateArgs(string message)
            {
                var args = new List<string>();
                var scanIndex = 0;
                var endIndex = message.Length;

                while (scanIndex < endIndex)
                {
                    var openBraceIndex = FindBraceIndex(message, '{', scanIndex, endIndex);
                    var closeBraceIndex = FindBraceIndex(message, '}', openBraceIndex, endIndex);

                    if (closeBraceIndex == endIndex)
                    {
                        scanIndex = endIndex;
                    }
                    else
                    {
                        // Format item syntax : { index[,alignment][ :formatString] }.
                        var formatDelimiterIndex = FindIndexOfAny(message, _formatDelimiters, openBraceIndex, closeBraceIndex);

                        args.Add(message.Substring(openBraceIndex + 1, formatDelimiterIndex - openBraceIndex - 1));
                        scanIndex = closeBraceIndex + 1;
                    }
                }

                return args;
            }

            private static int FindBraceIndex(string message, char brace, int startIndex, int endIndex)
            {
                // Example: {{prefix{{{Argument}}}suffix}}.
                var braceIndex = endIndex;
                var scanIndex = startIndex;
                var braceOccurrenceCount = 0;

                while (scanIndex < endIndex)
                {
                    if (braceOccurrenceCount > 0 && message[scanIndex] != brace)
                    {
                        if (braceOccurrenceCount % 2 == 0)
                        {
                            // Even number of '{' or '}' found. Proceed search with next occurrence of '{' or '}'.
                            braceOccurrenceCount = 0;
                            braceIndex = endIndex;
                        }
                        else
                        {
                            // An unescaped '{' or '}' found.
                            break;
                        }
                    }
                    else if (message[scanIndex] == brace)
                    {
                        if (brace == '}')
                        {
                            if (braceOccurrenceCount == 0)
                            {
                                // For '}' pick the first occurrence.
                                braceIndex = scanIndex;
                            }
                        }
                        else
                        {
                            // For '{' pick the last occurrence.
                            braceIndex = scanIndex;
                        }

                        braceOccurrenceCount++;
                    }

                    scanIndex++;
                }

                return braceIndex;
            }

            private static int FindIndexOfAny(string message, char[] chars, int startIndex, int endIndex)
            {
                var findIndex = message.IndexOfAny(chars, startIndex, endIndex - startIndex);
                return findIndex == -1 ? endIndex : findIndex;
            }
        }
    }
}
