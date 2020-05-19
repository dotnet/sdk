// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class ProgramTests
    {
        // Should be kept in sync with Program.Run
        // 
        private delegate void TestCommandHandlerDelegate(string project, string folder, string workspace, string verbosity, bool check, string[] include, string[] exclude, string report, bool includeGenerated);

        [Fact]
        public void ExitCodeIsOneWithCheckAndAnyFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 1, fileCount: 0, exitCode: 0);
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(Program.CheckFailedExitCode, exitCode);
        }

        [Fact]
        public void ExitCodeIsZeroWithCheckAndNoFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 42);
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void ExitCodeIsSameWithoutCheck()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 42);
            var exitCode = Program.GetExitCode(formatResult, check: false);

            Assert.Equal(formatResult.ExitCode, exitCode);
        }

        [Fact]
        public void CommandLine_OptionsAreParsedCorrectly()
        {
            // Arrange
            var sut = Program.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] {
                "--folder", "folder",
                "--workspace", "workspace",
                "--include", "include1", "include2",
                "--exclude", "exclude1", "exclude2",
                "--check",
                "--report", "report",
                "--verbosity", "verbosity",
                "--include-generated"});

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal(0, result.UnmatchedTokens.Count);
            Assert.Equal(0, result.UnparsedTokens.Count);
            Assert.Equal("folder", result.ValueForOption("folder"));
            Assert.Equal("workspace", result.ValueForOption("workspace"));
            Assert.Collection(result.ValueForOption<IEnumerable<string>>("include"), i0 => Assert.Equal("include1", i0), i1 => Assert.Equal("include2", i1));
            Assert.Collection(result.ValueForOption<IEnumerable<string>>("exclude"), i0 => Assert.Equal("exclude1", i0), i1 => Assert.Equal("exclude2", i1));
            Assert.True(result.ValueForOption<bool>("check"));
            Assert.Equal("report", result.ValueForOption("report"));
            Assert.Equal("verbosity", result.ValueForOption("verbosity"));
            Assert.True(result.ValueForOption<bool>("include-generated"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_Simple()
        {
            // Arrange
            var sut = Program.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "projectValue" });

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal("projectValue", result.CommandResult.GetArgumentValueOrDefault("project"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_WithOption_AfterArgument()
        {
            // Arrange
            var sut = Program.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "projectValue", "--verbosity", "verbosity" });

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal("projectValue", result.CommandResult.GetArgumentValueOrDefault("project"));
            Assert.Equal("verbosity", result.ValueForOption("verbosity"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_WithOption_BeforeArgument()
        {
            // Arrange
            var sut = Program.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "--verbosity", "verbosity", "projectValue" });

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal("projectValue", result.CommandResult.GetArgumentValueOrDefault("project"));
            Assert.Equal("verbosity", result.ValueForOption("verbosity"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_GetsPassedToHandler()
        {
            // Arrange
            var sut = Program.CreateCommandLineOptions();
            var handlerWasCalled = false;
            sut.Handler = CommandHandler.Create(new TestCommandHandlerDelegate(TestCommandHandler));
            
            void TestCommandHandler(string project, string folder, string workspace, string verbosity, bool check, string[] include, string[] exclude, string report, bool includeGenerated)
            {
                handlerWasCalled = true;
                Assert.Equal("projectValue", project);
                Assert.Equal("verbosity", verbosity);
            };
            
            // Act
            var result = sut.Invoke(new[] { "--verbosity", "verbosity", "projectValue" });

            // Assert
            Assert.True(handlerWasCalled);
        }
    }
}
