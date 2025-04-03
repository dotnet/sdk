// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;
using Microsoft.NET.TestFramework;

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
            var testInstance = _testAssetsManager
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
            var testInstance = _testAssetsManager
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
            var testInstance = _testAssetsManager
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
            var testInstance = _testAssetsManager
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
            var testInstance = _testAssetsManager
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
            var testInstance = _testAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_solutions", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path, "solution_b.sln");

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("solution_b.sln", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsProjectByFolder()
        {
            var testInstance = _testAssetsManager
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
            var testInstance = _testAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_projects", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path, "project_b.csproj");

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("project_b.csproj", solutionFileName);
            Assert.False(isSolution);
        }
    }
}
