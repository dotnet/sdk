// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Workspaces;

namespace Microsoft.CodeAnalysis.Tools.Tests.Workspaces
{
    public class CscCommandLineParserTests
    {
        [Fact]
        public void Parse_SimpleArgs_SplitsCorrectly()
        {
            var result = CscCommandLineParser.Parse("/noconfig /unsafe- /checked-");

            Assert.Equal(3, result.Length);
            Assert.Equal("/noconfig", result[0]);
            Assert.Equal("/unsafe-", result[1]);
            Assert.Equal("/checked-", result[2]);
        }

        [Fact]
        public void Parse_QuotedPath_PreservesSpaces()
        {
            var result = CscCommandLineParser.Parse("/out:\"C:\\Program Files\\output.dll\" /target:exe");

            Assert.Equal(2, result.Length);
            Assert.Equal("/out:C:\\Program Files\\output.dll", result[0]);
            Assert.Equal("/target:exe", result[1]);
        }

        [Fact]
        public void Parse_EmptyString_ReturnsEmpty()
        {
            var result = CscCommandLineParser.Parse("");

            Assert.Empty(result);
        }

        [Fact]
        public void Parse_MultipleSpaces_HandlesCorrectly()
        {
            var result = CscCommandLineParser.Parse("/a   /b    /c");

            Assert.Equal(3, result.Length);
            Assert.Equal("/a", result[0]);
            Assert.Equal("/b", result[1]);
            Assert.Equal("/c", result[2]);
        }

        [Fact]
        public void Parse_SourceFiles_IncludesFilePaths()
        {
            var result = CscCommandLineParser.Parse("/target:exe Program.cs Helpers.cs");

            Assert.Equal(3, result.Length);
            Assert.Equal("/target:exe", result[0]);
            Assert.Equal("Program.cs", result[1]);
            Assert.Equal("Helpers.cs", result[2]);
        }

        [Fact]
        public void Parse_DefineConstants_HandlesLongValue()
        {
            var result = CscCommandLineParser.Parse("/define:TRACE;DEBUG;NET;NET10_0");

            Assert.Single(result);
            Assert.Equal("/define:TRACE;DEBUG;NET;NET10_0", result[0]);
        }
    }
}
