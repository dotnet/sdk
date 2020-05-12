using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class FormattedFilesTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new FinalNewlineFormatter();

        private Dictionary<string, string> EditorConfig => new Dictionary<string, string>()
        {
            ["insert_final_newline"] = "true",
            ["end_of_line"] = "lf",
        };

        [Fact]
        public async Task ReturnsItem_WhenFileFormatted()
        {
            var testCode = "class C\n{\n}";

            var result = await TestFormattedFiles(testCode);

            Assert.Single(result);
        }

        [Fact]
        public async Task ReturnsEmptyList_WhenNoFilesFormatted()
        {
            var testCode = "class C\n{\n}\n";

            var result = await TestFormattedFiles(testCode);

            Assert.Empty(result);
        }

        private async Task<List<FormattedFile>> TestFormattedFiles(string testCode)
        {
            var text = SourceText.From(testCode, Encoding.UTF8);
            TestState.Sources.Add(text);

            var solution = GetSolution(TestState.Sources.ToArray(), TestState.AdditionalFiles.ToArray(), TestState.AdditionalReferences.ToArray());
            var project = solution.Projects.Single();
            var document = project.Documents.Single();
            var fileMatcher = SourceFileMatcher.CreateMatcher(new[] { document.FilePath }, exclude: Array.Empty<string>());
            var formatOptions = new FormatOptions(
                workspaceFilePath: project.FilePath,
                workspaceType: WorkspaceType.Folder,
                logLevel: LogLevel.Trace,
                saveFormattedFiles: false,
                changesAreErrors: false,
                fileMatcher,
                reportPath: string.Empty,
                includeGeneratedFiles: false);

            var pathsToFormat = await GetOnlyFileToFormatAsync(solution, EditorConfig);

            var formattedFiles = new List<FormattedFile>();
            await Formatter.FormatAsync(solution, pathsToFormat, formatOptions, new TestLogger(), formattedFiles, default);

            return formattedFiles;
        }
    }
}
