// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    [TestClass]
    public class CompositeWhitespaceFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new CompositeWhitespaceFormatter();

        [TestMethod]
        public async Task PreservesConstituentDiagnosticsAndFormatterFirstReporting()
        {
            var (workspace, solution) = await GetSolutionAsync(
                new[]
                {
                    ("First.cs", SourceText.From("class First\r\n{\r\n int x=1;\r\n}", new UTF8Encoding(false))),
                    ("Second.cs", SourceText.From("class Second\r\n{\r\n int x=2;\r\n}", new UTF8Encoding(false))),
                },
                Array.Empty<(string, SourceText)>(),
                Array.Empty<MetadataReference>(),
                new Dictionary<string, string>
                {
                    ["end_of_line"] = "lf",
                    ["insert_final_newline"] = "true",
                    ["charset"] = "utf-8-bom",
                });
            using (workspace)
            {
                var ids = solution.Projects.Single().DocumentIds.ToImmutableArray();
                var options = FormatOptions.Instance with { FixCategory = FixCategory.Whitespace, SaveFormattedFiles = false };
                var expectedFiles = new List<FormattedFile>();
                var expectedLogger = new TestLogger();
                var expected = solution;
                foreach (var formatter in new DocumentFormatter[] { new WhitespaceFormatter(), new FinalNewlineFormatter(), new EndOfLineFormatter(), new CharsetFormatter() })
                {
                    expected = await formatter.FormatAsync(workspace, expected, ids, options, expectedLogger, expectedFiles, TestContext.CancellationToken);
                }

                var actualFiles = new List<FormattedFile>();
                var actualLogger = new TestLogger();
                var actual = await Formatter.FormatAsync(workspace, solution, ids, options, actualLogger, actualFiles, TestContext.CancellationToken);

                foreach (var id in ids)
                {
                    var expectedText = await expected.GetDocument(id)!.GetTextAsync(TestContext.CancellationToken);
                    var actualText = await actual.GetDocument(id)!.GetTextAsync(TestContext.CancellationToken);
                    Assert.AreEqual(expectedText.ToString(), actualText.ToString());
                    Assert.AreEqual(expectedText.Encoding, actualText.Encoding);
                }

                expectedFiles.SelectMany(file => file.FileChanges).Select(change => change.DiagnosticId).Should().Contain("FINALNEWLINE");
                Assert.AreEqual(expectedLogger.GetLog(), actualLogger.GetLog());
                Assert.AreEqual(JsonSerializer.Serialize(expectedFiles), JsonSerializer.Serialize(actualFiles));
            }
        }

        [TestMethod]
        public async Task WhitespaceIsRetainedWithoutFinalNewlineSetting()
        {
            await AssertCodeChangedAsync(
                "class C\n{\n    int x=1;\n}",
                "class C\n{\n    int x = 1;\n}",
                new Dictionary<string, string> { ["end_of_line"] = "lf" });
        }

        [TestMethod]
        [DataRow("true", "\n")]
        [DataRow("false", "")]
        public async Task AppliesWhitespaceNewlineAndCharsetTogether(string insertFinalNewline, string suffix)
        {
            var text = await AssertCodeChangedAsync(
                "class C\r\n{\r\n    int x=1;\r\n}\r\n",
                "class C\n{\n    int x = 1;\n}" + suffix,
                new Dictionary<string, string>
                {
                    ["insert_final_newline"] = insertFinalNewline,
                    ["end_of_line"] = "lf",
                    ["charset"] = "utf-8-bom",
                },
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Assert.IsNotNull(text.Encoding);
            text.Encoding.GetPreamble().Should().Equal(new byte[] { 0xef, 0xbb, 0xbf });
        }
    }
}
