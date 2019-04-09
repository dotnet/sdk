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
            var formatResult = new WorkspaceFormatResult()
            {
                FilesFormatted = 1
            };
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(1, exitCode);
        }

        [Fact]
        public void ExitCodeIsZeroWithCheckAndNoFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult()
            {
                ExitCode = 42,
                FilesFormatted = 0
            };
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void ExitCodeIsSameWithoutCheck()
        {
            var formatResult = new WorkspaceFormatResult()
            {
                ExitCode = 42
            };
            var exitCode = Program.GetExitCode(formatResult, check: false);

            Assert.Equal(formatResult.ExitCode, exitCode);
        }

        [Fact]
        public void FilesFormattedDirectorySeparatorInsensitive()
        {
            var filePath = $"other_items{Path.DirectorySeparatorChar}OtherClass.cs";
            var files = Program.GetFilesToFormat(filePath);

            var filePathAlt = $"other_items{Path.AltDirectorySeparatorChar}OtherClass.cs";
            var filesAlt = Program.GetFilesToFormat(filePathAlt);

            Assert.True(files.IsSubsetOf(filesAlt));
        }
    }
}
