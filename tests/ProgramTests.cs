// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.CommandLine;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class ProgramTests
    {
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
        public void CommandLine_OptionsAreParedCorrectly()
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
    }
}
