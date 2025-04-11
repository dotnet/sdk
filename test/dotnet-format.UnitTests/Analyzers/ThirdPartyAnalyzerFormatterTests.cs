// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Tools.Analyzers;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Tests.XUnit;
using Microsoft.CodeAnalysis.Tools.Workspaces;

namespace Microsoft.CodeAnalysis.Tools.Tests.Analyzers
{
    public class ThirdPartyAnalyzerFormatterTests : CSharpFormatterTests, IAsyncLifetime
    {
        private static readonly string s_analyzerProjectFilePath = Path.Combine("for_analyzer_formatter", "analyzer_project", "analyzer_project.csproj");

        private protected override ICodeFormatter Formatter => AnalyzerFormatter.ThirdPartyFormatter;

        private Project _analyzerReferencesProject;

        public ThirdPartyAnalyzerFormatterTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        public async Task InitializeAsync()
        {
            var logger = new TestLogger();

            try
            {
                // Restore the Analyzer packages that have been added to `for_analyzer_formatter/analyzer_project/analyzer_project.csproj`
                var exitCode = await DotNetHelper.PerformRestoreAsync(s_analyzerProjectFilePath, TestOutputHelper);
                Assert.Equal(0, exitCode);

                // Load the analyzer_project into a MSBuildWorkspace.
                var workspacePath = Path.Combine(TestProjectsPathHelper.GetProjectsDirectory(), s_analyzerProjectFilePath);

                MSBuildRegistrar.RegisterInstance();
                var analyzerWorkspace = await MSBuildWorkspaceLoader.LoadAsync(workspacePath, WorkspaceType.Project, binaryLogPath: null, logWorkspaceWarnings: true, logger, CancellationToken.None);

                TestOutputHelper.WriteLine(logger.GetLog());

                // From this project we can get valid AnalyzerReferences to add to our test project.
                _analyzerReferencesProject = analyzerWorkspace.CurrentSolution.Projects.Single();
            }
            catch
            {
                TestOutputHelper.WriteLine(logger.GetLog());
                throw;
            }
        }

        public Task DisposeAsync()
        {
            _analyzerReferencesProject = null;

            return Task.CompletedTask;
        }

        private IEnumerable<AnalyzerReference> GetAnalyzerReferences(string prefix)
            => _analyzerReferencesProject.AnalyzerReferences.Where(reference => reference.Display.StartsWith(prefix));

        [MSBuildFact]
        public async Task TestStyleCopBlankLineFixer_RemovesUnnecessaryBlankLines()
        {
            var analyzerReferences = GetAnalyzerReferences("StyleCop");

            var testCode = @"
class C
{

    void M()

    {

        object obj = new object();


        int count = 5;

    }

}
";

            var expectedCode = @"
class C
{
    void M()
    {
        object obj = new object();

        int count = 5;
    }
}
";

            var editorConfig = new Dictionary<string, string>()
            {
                // Turn off all diagnostics analyzers
                ["dotnet_analyzer_diagnostic.severity"] = "none",

                // Two or more consecutive blank lines: Remove down to one blank line. SA1507
                ["dotnet_diagnostic.SA1507.severity"] = "error",

                // Blank line immediately before or after a { line: remove it. SA1505, SA1509
                ["dotnet_diagnostic.SA1505.severity"] = "error",
                ["dotnet_diagnostic.SA1509.severity"] = "error",

                // Blank line immediately before a } line: remove it. SA1508
                ["dotnet_diagnostic.SA1508.severity"] = "error",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig, fixCategory: FixCategory.Analyzers, analyzerReferences: analyzerReferences);
        }

        [MSBuildFact]
        public async Task TestIDisposableAnalyzer_AddsUsing()
        {
            var analyzerReferences = GetAnalyzerReferences("IDisposable");

            var testCode = @"
using System.IO;

class C
{
    void M()
    {
        var stream = File.OpenRead(string.Empty);
        var b = stream.ReadByte();
        stream.Dispose();
    }
}
";

            var expectedCode = @"
using System.IO;

class C
{
    void M()
    {
        using (var stream = File.OpenRead(string.Empty))
        {
            var b = stream.ReadByte();
        }
    }
}
";

            var editorConfig = new Dictionary<string, string>()
            {
                // Turn off all diagnostics analyzers
                ["dotnet_analyzer_diagnostic.severity"] = "none",

                // Prefer using. IDISP017
                ["dotnet_diagnostic.IDISP017.severity"] = "error",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig, fixCategory: FixCategory.Analyzers, analyzerReferences: analyzerReferences);
        }

        [MSBuildFact]
        public async Task TestLoadingAllAnalyzers_LoadsDependenciesFromAllSearchPaths()
        {
            // Loads all analyzer references.
            var analyzerReferences = _analyzerReferencesProject.AnalyzerReferences;

            var testCode = @"
using System.IO;

class C
{
    void M()
    {
        var stream = File.OpenRead(string.Empty);
        var b = stream.ReadByte();
        stream.Dispose();
    }
}
";

            var expectedCode = @"
using System.IO;

class C
{
    void M()
    {
        using (var stream = File.OpenRead(string.Empty))
        {
            var b = stream.ReadByte();
        }
    }
}
";

            var editorConfig = new Dictionary<string, string>()
            {
                // Turn off all diagnostics analyzers
                ["dotnet_analyzer_diagnostic.severity"] = "none",

                // Prefer using. IDISP017
                ["dotnet_diagnostic.IDISP017.severity"] = "error",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig, fixCategory: FixCategory.Analyzers, analyzerReferences: analyzerReferences);
        }
    }
}
