// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    [TestClass]
    public class DocumentFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new EndOfLineFormatter();

        [TestMethod]
        [DataRow("Component.razor")]
        [DataRow("Page.cshtml")]
        public async Task NormalizesRazorAdditionalDocuments(string fileName)
        {
            var (workspace, solution) = await GetSolutionAsync(
                Array.Empty<(string, SourceText)>(),
                new[] { (fileName, SourceText.From("<p>hello</p>\r\n", new UTF8Encoding(false))) },
                Array.Empty<MetadataReference>(),
                "root = true\n[*]\nend_of_line = lf\ninsert_final_newline = false\ncharset = utf-8-bom\n");
            using (workspace)
            {
                var documentId = solution.Projects.Single().AdditionalDocuments.Single().Id;
                solution = solution.RemoveAdditionalDocument(documentId).AddAdditionalDocument(
                    documentId, fileName, SourceText.From("<p>hello</p>\r\n", new UTF8Encoding(false)),
                    filePath: Path.Combine(DefaultFolderPath, fileName));
                var document = solution.GetAdditionalDocument(documentId)!;
                var files = new List<FormattedFile>();
                var formatted = await new CompositeWhitespaceFormatter().FormatAsync(
                    workspace, solution, ImmutableArray.Create(document.Id),
                    FormatOptions.Instance with { FixCategory = FixCategory.Whitespace },
                    new TestLogger(), files, TestContext.CancellationToken);
                var text = await formatted.GetAdditionalDocument(document.Id)!.GetTextAsync(TestContext.CancellationToken);

                Assert.AreEqual("<p>hello</p>", text.ToString());
                Assert.IsNotNull(text.Encoding);
                text.Encoding.GetPreamble().Should().Equal(new byte[] { 0xef, 0xbb, 0xbf });
                files.SelectMany(file => file.FileChanges).Select(change => change.DiagnosticId)
                    .Should().Contain("FINALNEWLINE").And.Contain("CHARSET");
            }
        }

        [TestMethod]
        public async Task SkipsMissingAndUnsupportedDocumentsAndPreservesReportOrder()
        {
            var (workspace, solution) = await GetSolutionAsync(
                new[]
                {
                    ("First.cs", SourceText.From("class First\r\n{\r\n}", Encoding.UTF8)),
                    ("Second.cs", SourceText.From("class Second\r\n{\r\n}", Encoding.UTF8)),
                },
                new[] { ("Notes.txt", SourceText.From("leave\r\nunchanged", Encoding.UTF8)) },
                Array.Empty<MetadataReference>(),
                new Dictionary<string, string> { ["end_of_line"] = "lf" });
            using (workspace)
            {
                var project = solution.Projects.Single();
                var first = project.Documents.First();
                var second = project.Documents.Last();
                var additional = project.AdditionalDocuments.Single();
                var ids = ImmutableArray.Create(
                    second.Id, DocumentId.CreateNewId(project.Id), additional.Id, first.Id);
                var files = new List<FormattedFile>();
                var formatted = await Formatter.FormatAsync(
                    workspace, solution, ids, FormatOptions.Instance with { FixCategory = FixCategory.Whitespace },
                    new TestLogger(), files, TestContext.CancellationToken);

                Assert.AreEqual("class First\n{\n}", (await formatted.GetDocument(first.Id)!.GetTextAsync(TestContext.CancellationToken)).ToString());
                Assert.AreEqual("class Second\n{\n}", (await formatted.GetDocument(second.Id)!.GetTextAsync(TestContext.CancellationToken)).ToString());
                Assert.AreEqual("leave\r\nunchanged", (await formatted.GetAdditionalDocument(additional.Id)!.GetTextAsync(TestContext.CancellationToken)).ToString());
                files.Select(file => file.DocumentId).Should().Equal(second.Id, first.Id);
            }
        }
    }
}
