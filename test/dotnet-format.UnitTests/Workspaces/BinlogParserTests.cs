// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Workspaces;

namespace Microsoft.CodeAnalysis.Tools.Tests.Workspaces
{
    public class BinlogParserTests
    {
        [Fact]
        public void ExtractCscInvocations_WithNonExistentFile_Throws()
        {
            Assert.ThrowsAny<Exception>(() =>
                BinlogParser.ExtractCscInvocations("/nonexistent/path/build.binlog"));
        }
    }
}
