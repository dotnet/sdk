// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.NetCore.Analyzers.Runtime;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.LoggerMessageDefineAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.Extensions.Logging.Analyzer
{
    [TestClass]
    public class LoggerMessageDefineTests
    {
        [TestMethod]
        [DynamicData(nameof(GenerateTemplateAndDefineUsages), @"{|CA2253:""{0}""|}", "1")]
        [DataRow(@"{|CA1848:logger.LogTrace({|CA2253:""{0}""|}, 1)|};")]
        [DataRow(@"{|CA1848:logger.LogTrace({|CA2253:""{0}""|}, ""1"")|};")]
        public async Task CA2253IsProducedForNumericFormatArgumentAsync(string format)
        {
            // Make sure CA1727 is enabled for this test so we can verify it does not trigger on numeric arguments.
            await TriggerCodeAsync(format);
        }

        [TestMethod]
        public async Task CA2254ShouldNotApplyForConstantValues()
        {
            await TriggerCodeAsync("{|CA1848:logger.LogDebug($\"{nameof(System.Collections.Generic.IList<object>)}<{{Arg1}}> could not be resovled from DI container, using default JSON binder.\", typeof(string).Assembly)|};");
        }

        [TestMethod]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2254:$""{new System.Exception().Message}""|}", "11", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2254:$""{string.Empty}""|}", "11", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2254:""string"" + 2|}", "11", false)]
        public async Task CA2254IsProducedForDynamicFormatArgumentAsync(string format)
        {
            await TriggerCodeAsync(format);
        }

        [TestMethod]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:""""|}", "1", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:{|CA1727:{|CA1727:""{string} {string}""|}|}|}", "1", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:{|CA1727:{|CA1727:""{string} {string}""|}|}|}", "new object[] { 1 }", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:{|CA1727:""{string}""|}|}", "1, 2", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:{|CA1727:""{string}""|}|}", "new object[] { 1 }, new object[] { 2 }", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:{|CA1727:""{str"" + ""ing}""|}|}", "1, 2", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:""{"" + nameof(ILogger) + ""}""|}", "", true)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA2017:{|CA1727:""{"" + Const + ""}""|}|}", "", true)]
        [DynamicData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2017:{|CA1727:""{string}""|}|}", 2)]
        [DynamicData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2017:{|CA1727:""{str"" + ""ing}""|}|}", 2)]
        [DynamicData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2017:""""|}", 1)]
        [DynamicData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2017:{|CA1727:{|CA1727:""{string} {string}""|}|}|}", 1)]
        [DynamicData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2017:""{"" + nameof(ILogger) + ""}""|}", 0)]
        [DynamicData(nameof(GenerateDefineUsagesWithExplicitNumberOfArgs), @"{|CA2017:{|CA1727:""{"" + Const + ""}""|}|}", 0)]
        public async Task CA2017IsProducedForFormatArgumentCountMismatchAsync(string format)
        {
            await TriggerCodeAsync(format);
        }

        [TestMethod]
        [DynamicData(nameof(GenerateTemplateAndDefineUsages), @"{|CA1727:""{camelCase}""|}", "1")]
        public async Task CA1727IsProducedForCamelCasedFormatArgumentAsync(string format)
        {
            await TriggerCodeAsync(format);
        }

        [TestMethod]
        // Concat would be optimized by compiler
        [DynamicData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @"nameof(ILogger) + "" string""", "")]
        [DynamicData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @""" string"" + "" string""", "")]
        [DynamicData(nameof(GenerateTemplateAndDefineUsageIgnoresCA1848ForBeginScope), @"$"" string"" + $"" string""", "")]
        [DynamicData(nameof(GenerateTemplateAndDefineUsages), @"{|CA1727:""{st"" + ""ring}""|}", "1")]

        // we are unable to parse expressions
        [DynamicData(nameof(GenerateTemplateArrayUsages), @"{|CA1727:{|CA1727:""{string} {string}""|}|}", "1", false)]
        [DynamicData(nameof(GenerateDefineUsages), @"{|CA1727:{|CA1727:""{string} {string}""|}|}")]

        // correct number of arguments
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA1727:""{string}""|}", "1", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA1727:{|CA1727:""{string} {string}""|}|}", "1, 2", false)]
        [DynamicData(nameof(GenerateTemplateUsages), @"{|CA1727:{|CA1727:""{string} {string}""|}|}", "new object[] { 1, 2 }", false)]

        // CA2253 is not enabled by default.
        [DynamicData(nameof(GenerateTemplateAndDefineUsages), @"{|CA1727:""{camelCase}""|}", "1")]
        public async Task TemplateDiagnosticsAreNotProducedAsync(string format)
        {
            await TriggerCodeAsync(format);
        }

        [TestMethod]
        [DataRow(@"LoggerMessage.Define(LogLevel.Information, 42, {|CA2017:""{One} {Two} {Three}""|});")]
        [DataRow(@"LoggerMessage.Define<int>(LogLevel.Information, 42, {|CA2017:""{One} {Two} {Three}""|});")]
        [DataRow(@"LoggerMessage.Define<int>(LogLevel.Information, 42, {|CA2017:""{One} {}""|});")]
        [DataRow(@"LoggerMessage.Define<int, int>(LogLevel.Information, 42, {|CA2017:""{One} {Two} {Three}""|});")]
        [DataRow(@"LoggerMessage.Define<int, int, int>(LogLevel.Information, 42, {|CA2017:""{One} {Two}""|});")]
        [DataRow(@"LoggerMessage.Define<int, int, int, int>(LogLevel.Information, 42, {|CA2017:""{One} {Two} {Three}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2017:""{One} {Two} {Three}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int, int>({|CA2017:""{One} {Two} {Three}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int, int, int>({|CA2017:""{One} {Two}""|});")]
        public async Task CA2017IsProducedForDefineMessageTypeParameterMismatchAsync(string expression)
        {
            await TriggerCodeAsync(expression);
        }

        [TestMethod]
        [WorkItem(7285, "https://github.com/dotnet/roslyn-analyzers/issues/7285")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""{{One}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""}{One}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""{One{Two}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""{One}{""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""{One}}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""{{{One}}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""}}{One}}""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""{{{One}{""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int>({|CA2023:""}}{One}{""|});")]
        [DataRow(@"LoggerMessage.DefineScope<int, int>({|CA2023:""}}{One} {Two}{""|});")]
        public async Task CA2023IsProducedWhenBracesAreInvalid(string format)
        {
            await TriggerCodeAsync(format);
        }

        [TestMethod]
        [DataRow("", true)]
        [DataRow(null, false)]
        [DataRow("{One}", true)]
        [DataRow("{One{", false)]
        [DataRow("{{One}", false)]
        [DataRow("{{One}}", true)]
        public void IsValidMessageTemplate_ShouldReturnExpectedResult(string messageTemplate, bool expectedResult)
        {
            var result = LoggerMessageDefineAnalyzer.IsValidMessageTemplate(messageTemplate);

            Assert.AreEqual(expectedResult, result);
        }

        [TestMethod]
        [WorkItem(7285, "https://github.com/dotnet/roslyn-analyzers/issues/7285")]
        [DataRow(@"LoggerMessage.DefineScope<int>(""Some logged value: {One}}} with an escaped brace"");")]
        [DataRow(@"LoggerMessage.DefineScope<int, int>(""}}Some logged value: {One}}} with an {Two}{{ escaped brace"");")]
        [DataRow(@"LoggerMessage.DefineScope<int, int>(""{{Some logged {{value: {One}}} with an {Two}{{{{ escaped brace{{}}"");")]
        public async Task CA2023IsNotProducedWhenBracesAreEscapedAndOtherwiseValid(string format)
        {
            await TriggerCodeAsync(format);
        }

        [TestMethod]
        [DataRow("LogTrace", @"""This is a test {Message}""")]
        [DataRow("LogDebug", @"""This is a test {Message}""")]
        [DataRow("LogInformation", @"""This is a test {Message}""")]
        [DataRow("LogWarning", @"""This is a test {Message}""")]
        [DataRow("LogError", @"""This is a test {Message}""")]
        [DataRow("LogCritical", @"""This is a test {Message}""")]
        [DataRow("BeginScope", @"""This is a test {Message}""")]
        public async Task CA1848IsProducedForInvocationsOfAllLoggerExtensionsAsync(string method, string template)
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

        public static IEnumerable<object[]> GenerateTemplateArrayUsages(string template, string arguments, bool ignoreCA1848ForBeginScope)
        {
            var arrayArgsDeclaration = $"var arrayArgs = new object[] {{ {arguments} }}";
            var templateAndArguments = $"{template}, arrayArgs";

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
                    yield return new[] { $"{arrayArgsDeclaration};\n{{|CA1848:logger.{method}({format}{templateAndArguments})|}};" };
                }
            }

            yield return ignoreCA1848ForBeginScope
               ? (new[] { $"{arrayArgsDeclaration};\nlogger.BeginScope({templateAndArguments});" })
               : (new[] { $"{arrayArgsDeclaration};\n{{|CA1848:logger.BeginScope({templateAndArguments})|}};" });
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
                TestCode = code,
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMELogging,
            }.RunAsync(CancellationToken.None);
        }
    }
}