// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests;

[TestClass]
public class TestAssetsManagerTests : SdkTest
{
    [TestMethod]
    [DataRow("Test")]
    [DataRow("TestWithALongNameThatRequiresHashing")]
    public void SameTestNameInDifferentFilesUsesSeparateDirectories(string testName)
    {
        var root = TestAssetsManager.CreateTestDirectory(identifier: testName);
        var first = TestAssetsManager.CreateTestDirectory(
            testName,
            identifier: "case",
            baseDirectory: root.Path,
            callerFilePath: Path.Join("source", "FirstTests.cs"));

        // An existing directory would make CI_BUILD add a suffix, masking a naming collision.
        Directory.Delete(first.Path);

        var second = TestAssetsManager.CreateTestDirectory(
            testName,
            identifier: "case",
            baseDirectory: root.Path,
            callerFilePath: Path.Join("source", "SecondTests.cs"));

        second.Path.Should().NotBe(first.Path);
        Path.GetDirectoryName(second.Path).Should().Be(root.Path);
        Path.GetFileName(first.Path).Length.Should().BeLessThanOrEqualTo(24);
        Path.GetFileName(second.Path).Length.Should().BeLessThanOrEqualTo(24);
    }
}
