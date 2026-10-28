// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.ToolPackage;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    [TestClass]
    public class LockFileMatcherTests
    {
        [TestMethod]
        [DataRow($"tools/{ToolsetInfo.CurrentTargetFramework}/any/tool.dll", "tool.dll", true)]
        [DataRow($@"tools\{ToolsetInfo.CurrentTargetFramework}\any\subDirectory\tool.dll", "subDirectory/tool.dll", true)]
        [DataRow($"tools/{ToolsetInfo.CurrentTargetFramework}/win-x64/tool.dll", "tool.dll", true)]
        [DataRow($"tools/{ToolsetInfo.CurrentTargetFramework}/any/subDirectory/tool.dll", "subDirectory/tool.dll", true)]
        [DataRow($"libs/{ToolsetInfo.CurrentTargetFramework}/any/tool.dll", "tool.dll", false)]
        [DataRow($"tools/{ToolsetInfo.CurrentTargetFramework}1/any/subDirectory/tool.dll", "tool.dll", false)]
        [DataRow($"tools/{ToolsetInfo.CurrentTargetFramework}/any/subDirectory/tool.dll", "subDirectory/subDirectory/subDirectory/subDirectory/subDirectory/tool.dll", false)]
        public void MatchesEntryPointTests(string pathInLockFileItem, string targetRelativeFilePath, bool shouldMatch)
        {
            LockFileMatcher.MatchesFile(new LockFileItem(pathInLockFileItem), targetRelativeFilePath)
                .Should().Be(shouldMatch);
        }


        [TestMethod]
        [DataRow($"tools/{ToolsetInfo.CurrentTargetFramework}/any/tool.dll", "", true)]
        [DataRow($@"tools\{ToolsetInfo.CurrentTargetFramework}\any\subDirectory\tool.dll", "subDirectory", true)]
        [DataRow($@"tools\{ToolsetInfo.CurrentTargetFramework}\any\subDirectory\tool.dll", "sub", false)]
        [DataRow($"tools/{ToolsetInfo.CurrentTargetFramework}/any/subDirectory/tool.dll", "any/subDirectory", false)]
        public void MatchesDirectoryPathTests(
            string pathInLockFileItem,
            string targetRelativeFilePath,
            bool shouldMatch)
        {
            LockFileMatcher.MatchesDirectoryPath(new LockFileItem(pathInLockFileItem), targetRelativeFilePath)
                .Should().Be(shouldMatch);
        }
    }
}
