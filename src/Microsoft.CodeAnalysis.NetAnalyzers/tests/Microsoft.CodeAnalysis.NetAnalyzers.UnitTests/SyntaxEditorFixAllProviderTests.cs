// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    /// <summary>
    /// Covers <see cref="SyntaxEditorFixAllProvider.Order"/> directly. The order a document's diagnostics
    /// are handed to the fix in decides whether a fix can see the edits made before it, and the fixers that
    /// depend on it reach it through several layers of test harness.
    /// </summary>
    [TestClass]
    public class SyntaxEditorFixAllProviderTests
    {
        //  RS2008 tracks rules that actually ship. This descriptor exists only to give the fabricated
        //  diagnostics below an id and a location; no analyzer reports it.
#pragma warning disable RS2008 // Enable analyzer release tracking
        private static readonly DiagnosticDescriptor s_descriptor = new(
            "TEST0001",
            "Title",
            "Message",
            "Category",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
#pragma warning restore RS2008

        private static Diagnostic DiagnosticAt(int start, int length)
        {
            var span = new TextSpan(start, length);

            return Diagnostic.Create(
                s_descriptor,
                Location.Create(
                    "test.cs",
                    span,
                    new LinePositionSpan(new LinePosition(0, start), new LinePosition(0, start + length))));
        }

        private static string Starts(IEnumerable<Diagnostic> diagnostics)
            => string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Location.SourceSpan.Start));

        [TestMethod]
        public void DisjointSpansSortByStart()
        {
            ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(
                DiagnosticAt(30, 5),
                DiagnosticAt(10, 5),
                DiagnosticAt(20, 5));

            Assert.AreEqual("10, 20, 30", Starts(SyntaxEditorFixAllProvider.Order(diagnostics)));
        }

        [TestMethod]
        public void NestedSpansSortInnermostFirst()
        {
            //  Replacing an enclosing node discards the editor's tracking of everything inside it, so a
            //  document whose spans nest has to be fixed innermost-first rather than in source order.
            ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(
                DiagnosticAt(10, 30),
                DiagnosticAt(15, 10),
                DiagnosticAt(50, 5));

            Assert.AreEqual("50, 15, 10", Starts(SyntaxEditorFixAllProvider.Order(diagnostics)));
        }

        [TestMethod]
        public void EqualSpansAreNotNesting()
        {
            //  Several diagnostics reported at one node are fixed against one target, so they do not force the
            //  document onto the innermost-first path.
            ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(
                DiagnosticAt(20, 5),
                DiagnosticAt(10, 5),
                DiagnosticAt(10, 5));

            Assert.AreEqual("10, 10, 20", Starts(SyntaxEditorFixAllProvider.Order(diagnostics)));
        }

        [TestMethod]
        public void AdjacentSpansAreNotNesting()
        {
            //  A span that starts where the one before it ends is the next sibling, not a child.
            ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(
                DiagnosticAt(10, 10),
                DiagnosticAt(20, 10));

            Assert.AreEqual("10, 20", Starts(SyntaxEditorFixAllProvider.Order(diagnostics)));
        }

        [TestMethod]
        public void TheShorterOfTwoSpansSharingAStartComesFirst()
        {
            //  An invocation and the member access it is built from begin at the same offset, so the end is
            //  what says which of the two is inside the other.
            ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(
                DiagnosticAt(10, 20),
                DiagnosticAt(10, 5));

            IEnumerable<Diagnostic> ordered = SyntaxEditorFixAllProvider.Order(diagnostics);

            Assert.AreEqual("15, 30", string.Join(", ", ordered.Select(diagnostic => diagnostic.Location.SourceSpan.End)));
        }

        [TestMethod]
        public async Task ApplyFixesAsyncFixesADuplicatedDiagnosticOnceAsync()
        {
            using var workspace = new AdhocWorkspace();

            Project project = workspace.AddProject("P", LanguageNames.CSharp);
            Document document = workspace.AddDocument(project.Id, "test.cs", SourceText.From("class C { }"));
            int applied = 0;

            //  The host reports the same diagnostic twice as separate instances, and a fix applied twice to one
            //  node throws.
            await SyntaxEditorFixAllProvider.ApplyFixesAsync(
                document,
                ImmutableArray.Create(DiagnosticAt(0, 5), DiagnosticAt(0, 5)),
                (doc, diagnostic, editor, cancellationToken) =>
                {
                    applied++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.AreEqual(1, applied);
        }

        [TestMethod]
        public async Task ApplyFixesAsyncDoesNotBindASemanticModelAsync()
        {
            using var workspace = new AdhocWorkspace();

            Project project = workspace.AddProject("P", LanguageNames.CSharp);
            Document document = workspace.AddDocument(project.Id, "test.cs", SourceText.From("class C { }"));

            Assert.IsFalse(document.TryGetSemanticModel(out _), "the document should start unbound");

            //  DocumentEditor.CreateAsync binds a semantic model whether or not the fix reads one, which the
            //  majority of fixes do not. A fix that needs semantics asks the document for the model itself.
            await SyntaxEditorFixAllProvider.ApplyFixesAsync(
                document,
                ImmutableArray.Create(DiagnosticAt(0, 5)),
                (doc, diagnostic, editor, cancellationToken) => Task.CompletedTask,
                CancellationToken.None);

            Assert.IsFalse(document.TryGetSemanticModel(out _));
        }

        [TestMethod]
        public void ASingleDiagnosticIsNotNesting()
        {
            ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(DiagnosticAt(10, 5));

            Assert.AreEqual("10", Starts(SyntaxEditorFixAllProvider.Order(diagnostics)));
        }
    }
}
