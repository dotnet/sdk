// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class WorkspacesUtilitiesTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow(LanguageNames.CSharp, "throw null;")]
        [DataRow(LanguageNames.VisualBasic, "Throw Nothing")]
        public async Task DefaultMethodStatementFallsBackToThrowingNullAsync(string language, string expected)
        {
            using var workspace = new AdhocWorkspace();
            Project project = workspace.AddProject("P", language);
            Compilation compilation = await project.GetCompilationAsync(TestContext.CancellationToken);
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(workspace, language);

            SyntaxNode statement = generator.DefaultMethodStatement(compilation);

            Assert.AreEqual(expected, statement.NormalizeWhitespace().ToFullString());
        }
    }
}
