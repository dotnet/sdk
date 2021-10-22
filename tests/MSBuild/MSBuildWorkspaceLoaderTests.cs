// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Tests.XUnit;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Tools.Tests.MSBuild
{
    public class MSBuildWorkspaceLoaderTests
    {
        private static string ProjectsPath => TestProjectsPathHelper.GetProjectsDirectory();

        protected ITestOutputHelper TestOutputHelper { get; set; }

        public MSBuildWorkspaceLoaderTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        [MSBuildTheory(typeof(WindowsOnly))]
        [InlineData("winforms")]
        [InlineData("winformslib")]
        [InlineData("wpf")]
        [InlineData("wpflib")]
        [InlineData("wpfcustomcontrollib")]
        [InlineData("wpfusercontrollib")]
        public async Task CSharpTemplateProject_WindowsOnly_LoadWithNoDiagnostics(string templateName)
        {
            await AssertTemplateProjectLoadsCleanlyAsync(templateName, LanguageNames.CSharp);
        }

        [MSBuildTheory]
        [InlineData("web")]
        [InlineData("grpc")]
        [InlineData("webapi")]
        [InlineData("razor")]
        [InlineData("mvc")]
        [InlineData("angular")]
        [InlineData("react")]
        [InlineData("reactredux")]
        [InlineData("blazorserver")]
        [InlineData("blazorwasm")]
        [InlineData("classlib")]
        [InlineData("console")]
        [InlineData("mstest")]
        [InlineData("nunit")]
        [InlineData("razorclasslib")]
        [InlineData("worker")]
        [InlineData("xunit")]
        public async Task CSharpTemplateProject_LoadWithNoDiagnostics(string templateName)
        {
            await AssertTemplateProjectLoadsCleanlyAsync(templateName, LanguageNames.CSharp);
        }

        [MSBuildTheory]
        [InlineData("classlib")]
        [InlineData("console")]
        [InlineData("mstest")]
        [InlineData("nunit")]
        [InlineData("xunit")]
        public async Task VisualBasicTemplateProject_LoadWithNoDiagnostics(string templateName)
        {
            await AssertTemplateProjectLoadsCleanlyAsync(templateName, LanguageNames.VisualBasic, ignoredDiagnostics: new[] { "BC30002" });
        }

        private async Task AssertTemplateProjectLoadsCleanlyAsync(string templateName, string languageName, string[] ignoredDiagnostics = null)
        {
            var logger = new TestLogger();

            try
            {
                if (ignoredDiagnostics is not null)
                {
                    TestOutputHelper.WriteLine($"Ignoring compiler diagnostics: \"{string.Join("\", \"", ignoredDiagnostics)}\"");
                }

                // Clean up previous run
                CleanupProject(templateName, languageName);

                var projectFilePath = await GenerateProjectFromTemplateAsync(templateName, languageName, TestOutputHelper);

                await AssertProjectLoadsCleanlyAsync(projectFilePath, logger, ignoredDiagnostics);

                // Clean up successful run
                CleanupProject(templateName, languageName);
            }
            catch
            {
                TestOutputHelper.WriteLine(logger.GetLog());
                throw;
            }
        }

        private static async Task<string> GenerateProjectFromTemplateAsync(string templateName, string languageName, ITestOutputHelper outputHelper)
        {
            var projectPath = GetProjectPath(templateName, languageName);
            var projectFilePath = GetProjectFilePath(projectPath, languageName);

            var exitCode = await DotNetHelper.NewProject(templateName, projectPath, languageName, outputHelper);
            Assert.Equal(0, exitCode);

            return projectFilePath;
        }

        private static async Task AssertProjectLoadsCleanlyAsync(string projectFilePath, ILogger logger, string[] ignoredDiagnostics)
        {
            var binaryLogPath = Path.ChangeExtension(projectFilePath, ".binlog");

            MSBuildRegistrar.RegisterInstance();
            using var workspace = (MSBuildWorkspace)await MSBuildWorkspaceLoader.LoadAsync(projectFilePath, WorkspaceType.Project, binaryLogPath, logWorkspaceWarnings: true, logger, CancellationToken.None);

            Assert.Empty(workspace.Diagnostics);

            var project = workspace.CurrentSolution.Projects.Single();
            var compilation = await project.GetCompilationAsync();

            // Unnecessary using directives are reported with a severty of Hidden
            var diagnostics = compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity > DiagnosticSeverity.Hidden && ignoredDiagnostics?.Contains(diagnostic.Id) != true);

            Assert.Empty(diagnostics);
        }

        private static void CleanupProject(string templateName, string languageName)
        {
            var projectPath = GetProjectPath(templateName, languageName);

            if (Directory.Exists(projectPath))
            {
                Directory.Delete(projectPath, true);
            }
        }

        private static string GetProjectPath(string templateName, string languageName)
        {
            var languagePrefix = languageName.Replace("#", "Sharp").Replace(' ', '_').ToLower();
            var projectName = $"{languagePrefix}_{templateName}_project";
            return Path.Combine(ProjectsPath, "for_workspace_loader", projectName);
        }

        private static string GetProjectFilePath(string projectPath, string languageName)
        {
            var projectName = Path.GetFileName(projectPath);
            var projectExtension = languageName switch
            {
                LanguageNames.CSharp => "csproj",
                LanguageNames.VisualBasic => "vbproj",
                _ => throw new ArgumentOutOfRangeException(nameof(languageName), actualValue: languageName, message: "Only C# and VB.Net project are supported.")
            };
            return Path.Combine(projectPath, $"{projectName}.{projectExtension}");
        }
    }
}
