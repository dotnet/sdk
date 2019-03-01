// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class MSBuildWorkspaceFinderTests
    {
        private static string SolutionPath => Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.Parent.FullName;

        [Theory]
        [InlineData("Could not find a MSBuild project file or solution file in ", "tests/projects/for_workspace_finder/no_project_or_solution/")]
        [InlineData("Multiple MSBuild project files found in ", "tests/projects/for_workspace_finder/multiple_projects/")]
        [InlineData("Multiple MSBuild solution files found in ", "tests/projects/for_workspace_finder/multiple_solutions/")]
        [InlineData("Both a MSBuild project file and solution file found in ", "tests/projects/for_workspace_finder/project_and_solution/")]
        public void ThrowsExceptions(string exceptionMessageStart, string workspacePath)
        {
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, workspacePath));

            Assert.StartsWith(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void FindsSolutionByFolder()
        {
            const string Path = "tests/projects/for_workspace_finder/single_solution/";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("single_solution.sln", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsSolutionByFilePath()
        {
            const string Path = "tests/projects/for_workspace_finder/multiple_solutions/solution_b.sln";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("solution_b.sln", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsProjectByFolder()
        {
            const string Path = "tests/projects/for_workspace_finder/single_project/";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("single_project.csproj", solutionFileName);
            Assert.False(isSolution);
        }

        [Fact]
        public void FindsProjectByFilePath()
        {
            const string Path = "tests/projects/for_workspace_finder/multiple_projects/project_b.csproj";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("project_b.csproj", solutionFileName);
            Assert.False(isSolution);
        }
    }
}
