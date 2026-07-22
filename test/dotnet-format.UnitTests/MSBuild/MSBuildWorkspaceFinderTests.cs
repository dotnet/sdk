// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Workspaces;

namespace Microsoft.CodeAnalysis.Tools.Tests.MSBuild
{
    [TestClass]
    public class MSBuildWorkspaceFinderTests : SdkTest
    {

        public MSBuildWorkspaceFinderTests()
        {
        }

        private string ProjectsPath => TestProjectsPathHelper.GetProjectsDirectory();

        [TestMethod]
        public void ThrowsException_CannotFindMSBuildProjectFile()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/no_project_or_solution", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Could_not_find_a_MSBuild_project_or_solution_file_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.ThrowsExactly<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.StartsWith(exceptionMessageStart, exception.Message);
        }

        [TestMethod]
        public void ThrowsException_MultipleMSBuildProjectFiles()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_projects", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Multiple_MSBuild_project_files_found_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.ThrowsExactly<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.AreEqual(exceptionMessageStart, exception.Message);
        }

        [TestMethod]
        public void ThrowsException_MultipleMSBuildSolutionFiles()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_solutions", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Multiple_MSBuild_solution_files_found_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.ThrowsExactly<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.AreEqual(exceptionMessageStart, exception.Message);
        }

        [TestMethod]
        public void ThrowsException_SolutionAndProjectAmbiguity()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/project_and_solution", testAssetSubdirectory: "dotnet-format")
                .WithSource();
            var exceptionMessageStart = string.Format(
                Resources.Both_a_MSBuild_project_file_and_solution_file_found_in_0_Specify_which_to_use_with_the_workspace_argument,
                testInstance.Path).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.ThrowsExactly<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path));
            Assert.AreEqual(exceptionMessageStart, exception.Message);
        }

        [TestMethod]
        public void FindsSolutionByFolder()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/single_solution", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.AreEqual("single_solution.sln", solutionFileName);
            Assert.IsTrue(isSolution);
        }

        [TestMethod]
        public void FindsSolutionByFilePath()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_solutions", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path, "solution_b.sln");

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.AreEqual("solution_b.sln", solutionFileName);
            Assert.IsTrue(isSolution);
        }

        [TestMethod]
        public void FindsSlnxByFolder()
        {
            const string Path = "for_workspace_finder/single_slnx/";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(ProjectsPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.AreEqual("single_slnx.slnx", solutionFileName);
            Assert.IsTrue(isSolution);
        }

        [TestMethod]
        public void FindsSlnxByFilePath()
        {
            const string Path = "for_workspace_finder/multiple_solutions/solution_c.slnx";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(ProjectsPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.AreEqual("solution_c.slnx", solutionFileName);
            Assert.IsTrue(isSolution);
        }

        [TestMethod]
        public void FindsProjectByFolder()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/single_project", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.AreEqual("single_project.csproj", solutionFileName);
            Assert.IsFalse(isSolution);
        }

        [TestMethod]
        public void FindsProjectByFilePath()
        {
            var testInstance = TestAssetsManager
                .CopyTestAsset(testProjectName: "for_workspace_finder/multiple_projects", testAssetSubdirectory: "dotnet-format")
                .WithSource();

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(testInstance.Path, "project_b.csproj");

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.AreEqual("project_b.csproj", solutionFileName);
            Assert.IsFalse(isSolution);
        }
    }
}
