// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.IO;
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
    }
}
