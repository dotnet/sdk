// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

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

        public FormattedFilesTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

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

            var (workspace, solution) = await GetSolutionAsync(TestState.Sources.ToArray(), TestState.AdditionalFiles.ToArray(), TestState.AdditionalReferences.ToArray(), EditorConfig);
            var project = solution.Projects.Single();
            var document = project.Documents.Single();

            var fileMatcher = SourceFileMatcher.CreateMatcher(new[] { document.FilePath }, exclude: Array.Empty<string>());
            var formatOptions = new FormatOptions(
                WorkspaceFilePath: project.FilePath,
                WorkspaceType: WorkspaceType.Folder,
                NoRestore: false,
                LogLevel: LogLevel.Trace,
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                Diagnostics: ImmutableHashSet<string>.Empty,
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                fileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false,
                BinaryLogPath: null);

            var pathsToFormat = GetOnlyFileToFormat(solution);

            var formattedFiles = new List<FormattedFile>();
            await Formatter.FormatAsync(workspace, solution, pathsToFormat, formatOptions, new TestLogger(), formattedFiles, default);

            return formattedFiles;
        }
    }
}
