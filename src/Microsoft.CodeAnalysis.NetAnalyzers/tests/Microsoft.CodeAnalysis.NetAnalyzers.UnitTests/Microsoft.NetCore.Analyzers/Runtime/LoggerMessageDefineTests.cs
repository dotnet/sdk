// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information. 

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.LoggerMessageDefineAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.Extensions.Logging.Analyzer
{
    public class LoggerMessageDefineTests
    {
        [Theory]
        [MemberData(nameof(GenerateTemplateAndDefineUsages), @"{|CA2253:""{0}""|}", "1")]
        public async Task CA2253IsProducedForNumericFormatArgument(string format)
        {
            // Make sure CA1727 is enabled for this test so we can verify it does not trigger on numeric arguments.
            await TriggerCodeAsync(format);
        }

        [Theory]
        [MemberData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @"{|CA2254:$""{string.Empty}""|}", "")]
        [MemberData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @"{|CA2254:""string"" + 2|}", "")]
        public async Task CA2254IsProducedForDynamicFormatArgument(string format)
        {
            await TriggerCodeAsync(format);
        }

        [Theory]
        [MemberData(nameof(GenerateTemplateUsages), @"{|CA2255:{|CA1727:""{string}""|}|}", "1, 2", false)]
        [MemberData(nameof(GenerateTemplateUsages), @"{|CA2255:{|CA1727:""{str"" + ""ing}""|}|}", "1, 2", false)]
        [MemberData(nameof(GenerateTemplateUsages), @"{|CA2255:""{"" + nameof(ILogger) + ""}""|}", "", true)]
        [MemberData(nameof(GenerateTemplateUsages), @"{|CA2255:{|CA1727:""{"" + Const + ""}""|}|}", "", true)]
        [MemberData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2255:{|CA1727:""{string}""|}|}", 2)]
        [MemberData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2255:{|CA1727:""{str"" + ""ing}""|}|}", 2)]
        [MemberData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2255:""{"" + nameof(ILogger) + ""}""|}", 0)]
        [MemberData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2255:{|CA1727:""{"" + Const + ""}""|}|}", 0)]
        public async Task CA2255IsProducedForFormatArgumentCountMismatch(string format)
        {
            await TriggerCodeAsync(format);
        }

        [Theory]
        [MemberData(nameof(GenerateTemplateAndDefineUsages), @"{|CA1727:""{camelCase}""|}", "1")]
        public async Task CA1727IsProducedForCamelCasedFormatArgument(string format)
        {
            await TriggerCodeAsync(format);
        }

        [Theory]
        // Concat would be optimized by compiler
        [MemberData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @"nameof(ILogger) + "" string""", "")]
        [MemberData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @""" string"" + "" string""", "")]
        [MemberData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @"$"" string"" + $"" string""", "")]
        [MemberData(nameof(GenerateTemplateAndDefineUsages), @"{|CA1727:""{st"" + ""ring}""|}", "1")]

        // we are unable to parse expressions
        [MemberData(nameof(GenerateTemplateUsages), @"{|CA2255:{|CA1727:{|CA1727:""{string} {string}""|}|}|}", "new object[] { 1 }", false)]
        [MemberData(nameof(GenerateDefineUsages), @"{|CA1727:{|CA1727:""{string} {string}""|}|}")]

        // CA2253 is not enabled by default.
        [MemberData(nameof(GenerateTemplateAndDefineUsages), @"{|CA1727:""{camelCase}""|}", "1")]
        public async Task TemplateDiagnosticsAreNotProduced(string format)
        {
            await TriggerCodeAsync(format);
        }

        [Theory]
        [InlineData(@"LoggerMessage.Define(LogLevel.Information, 42, {|CA2255:""{One} {Two} {Three}""|});")]
        [InlineData(@"LoggerMessage.Define<int>(LogLevel.Information, 42, {|CA2255:""{One} {Two} {Three}""|});")]
        [InlineData(@"LoggerMessage.Define<int, int>(LogLevel.Information, 42, {|CA2255:""{One} {Two} {Three}""|});")]
        [InlineData(@"LoggerMessage.Define<int, int, int>(LogLevel.Information, 42, {|CA2255:""{One} {Two}""|});")]
        [InlineData(@"LoggerMessage.Define<int, int, int, int>(LogLevel.Information, 42, {|CA2255:""{One} {Two} {Three}""|});")]
        [InlineData(@"LoggerMessage.DefineScope<int>({|CA2255:""{One} {Two} {Three}""|});")]
        [InlineData(@"LoggerMessage.DefineScope<int, int>({|CA2255:""{One} {Two} {Three}""|});")]
        [InlineData(@"LoggerMessage.DefineScope<int, int, int>({|CA2255:""{One} {Two}""|});")]
        public async Task CA2255IsProducedForDefineMessageTypeParameterMismatch(string expression)
        {
            await TriggerCodeAsync(expression);
        }

        [Theory]
        [InlineData("LogTrace", @"""This is a test {Message}""")]
        [InlineData("LogDebug", @"""This is a test {Message}""")]
        [InlineData("LogInformation", @"""This is a test {Message}""")]
        [InlineData("LogWarning", @"""This is a test {Message}""")]
        [InlineData("LogError", @"""This is a test {Message}""")]
        [InlineData("LogCritical", @"""This is a test {Message}""")]
        [InlineData("BeginScope", @"""This is a test {Message}""")]
        public async Task CA1848IsProducedForInvocationsOfAllLoggerExtensions(string method, string template)
        {
            var expression = @$"{{|CA1848:logger.{method}({template},""Foo"")|}};";
            await TriggerCodeAsync(expression);
        }

        public static IEnumerable<object[]> GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope(string template, string arguments)
        {
            return GenerateTemplateUsages(template, arguments, ignoreCA1848ForBeginScope: true).Concat(GenerateDefineUsages(template));
        }

        public static IEnumerable<object[]> GenerateTemplateAndDefineUsages(string template, string arguments)
        {
            return GenerateTemplateUsages(template, arguments, ignoreCA1848ForBeginScope: false).Concat(GenerateDefineUsages(template));
        }

        public static IEnumerable<object[]> GenerateDefineUsages(string template)
        {
            return GenerateDefineUsagesWithExplicitNumberOfArgs(template, numArgs: -1);
        }

        public static IEnumerable<object[]> GenerateDefineUsagesWithExplicitNumberOfArgs(string template, int numArgs)
        {
            TestFileMarkupParser.GetSpans(template, out _, out ImmutableDictionary<string, ImmutableArray<TextSpan>> spans);
            var spanCount = spans.Sum(pair => string.IsNullOrEmpty(pair.Key) ? 0 : pair.Value.Count());

            var numberOfArguments = template.Count(c => c == '{') - spanCount;
            if (numArgs != -1)
            {
                numberOfArguments = numArgs;
            }

            yield return new[] { $"LoggerMessage.{GenerateGenericInvocation(numberOfArguments, "DefineScope")}({template});" };
            yield return new[] { $"LoggerMessage.{GenerateGenericInvocation(numberOfArguments, "Define")}(LogLevel.Information, 42, {template});" };
        }

        public static IEnumerable<object[]> GenerateTemplateUsages(string template, string arguments, bool ignoreCA1848ForBeginScope)
        {
            var templateAndArguments = template;
            if (!string.IsNullOrEmpty(arguments))
            {
                templateAndArguments = $"{template}, {arguments}";
            }
            var methods = new[] { "LogTrace", "LogError", "LogWarning", "LogInformation", "LogDebug", "LogCritical" };
            var formats = new[]
            {
                "",
                "0, ",
                "1, new System.Exception(), ",
                "2, null, "
            };
            foreach (var method in methods)
            {
                foreach (var format in formats)
                {
                    yield return new[] { $"{{|CA1848:logger.{method}({format}{templateAndArguments})|}};" };
                }
            }

            yield return ignoreCA1848ForBeginScope
               ? (new[] { $"logger.BeginScope({templateAndArguments});" })
               : (new[] { $"{{|CA1848:logger.BeginScope({templateAndArguments})|}};" });
        }

        private static string GenerateGenericInvocation(int i, string method)
        {
            if (i > 0)
            {
                var types = string.Join(", ", Enumerable.Range(0, i).Select(_ => "int"));
                method += $"<{types}>";
            }

            return method;
        }

        private async Task TriggerCodeAsync(string expression)
        {
            string code = @$"
using Microsoft.Extensions.Logging;
public class Program
{{
    public const string Const = ""const"";
    public static void Main()
    {{
        ILogger logger = null;
        {expression}
    }}
}}";
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                TestState =
                {
                    Sources = { code }
                },
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            }.RunAsync();
        }
    }
}