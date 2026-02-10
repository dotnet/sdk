// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;

namespace Microsoft.CodeAnalysis.Tools.Tests.MSBuild
{
    public class MSBuildWorkspaceFinderTests : SdkTest
    {

        public MSBuildWorkspaceFinderTests(ITestOutputHelper log) : base(log)
        {
        }

        private string ProjectsPath => TestProjectsPathHelper.GetProjectsDirectory();

        [Fact]
        public void ThrowsException_CannotFindMSBuildProjectFile()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/no_project_or_solution", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Could_not_find_a_MSBuild_project_or_solution_file_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.StartsWith(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void ThrowsException_MultipleMSBuildProjectFiles()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_projects", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Multiple_MSBuild_project_files_found_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.Equal(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void ThrowsException_MultipleMSBuildSolutionFiles()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_solutions", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Multiple_MSBuild_solution_files_found_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.Equal(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void ThrowsException_SolutionAndProjectAmbiguity()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/project_and_solution", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Both_a_MSBuild_project_file_and_solution_file_found_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.Equal(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void FindsSolutionByFolder()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/single_solution", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("single_solution.sln", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsSolutionByFilePath()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_solutions", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path, "solution_b.sln");

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("solution_b.sln", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsSlnxByFolder()
        {
            const string Path = "for_workspace_finder/single_slnx/";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(ProjectsPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("single_slnx.slnx", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsSlnxByFilePath()
        {
            const string Path = "for_workspace_finder/multiple_solutions/solution_c.slnx";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(ProjectsPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("solution_c.slnx", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsProjectByFolder()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/single_project", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("single_project.csproj", solutionFileName);
            Assert.False(isSolution);
        }

        [Fact]
        public void FindsProjectByFilePath()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_projects", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path, "project_b.csproj");

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("project_b.csproj", solutionFileName);
            Assert.False(isSolution);
        }
    }
}
