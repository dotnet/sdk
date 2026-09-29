// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Workload.Install.Tests;

[TestClass]
public class CorruptWorkloadSetTestHelperTests : SdkTest
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SetupUsesCallingTestIdentity(bool userLocal)
    {
        var expectedDirectory = TestAssetsManager.CreateTestDirectory(identifier: userLocal ? "userlocal" : "default");
        Directory.Delete(expectedDirectory.Path);

        var setup = CorruptWorkloadSetTestHelper.SetupCorruptWorkloadSet(TestAssetsManager, userLocal, out _);

        Path.GetDirectoryName(setup.dotnetRoot).Should().Be(expectedDirectory.Path);
    }

    [TestMethod]
    [CombinatorialData]
    public void SetupPreservesOtherCallersFiles(
        bool userLocal,
        [CombinatorialValues("OtherTest", "SameTest")] string secondTestName)
    {
        var firstTestName = nameof(SetupPreservesOtherCallersFiles) + "SameTest";
        var firstFile = "FirstTests.cs";
        var secondFile = secondTestName == "SameTest" ? "SecondTests.cs" : firstFile;
        secondTestName = nameof(SetupPreservesOtherCallersFiles) + secondTestName;
        var identifier = userLocal ? "userlocal" : "default";
        var expectedFirst = TestAssetsManager.CreateTestDirectory(firstTestName, identifier, callerFilePath: firstFile);
        var expectedSecond = TestAssetsManager.CreateTestDirectory(secondTestName, identifier, callerFilePath: secondFile);

        // Remove the empty placeholders so CI_BUILD's retry suffix cannot hide missing caller forwarding.
        Directory.Delete(expectedFirst.Path);
        Directory.Delete(expectedSecond.Path);

        var first = CorruptWorkloadSetTestHelper.SetupCorruptWorkloadSet(
            TestAssetsManager, userLocal, out _, testName: firstTestName, callerFilePath: firstFile);
        Path.GetDirectoryName(first.dotnetRoot).Should().Be(expectedFirst.Path);
        var markerPath = Path.Join(first.dotnetRoot, "marker.txt");
        File.WriteAllText(markerPath, "first caller");

        var second = CorruptWorkloadSetTestHelper.SetupCorruptWorkloadSet(
            TestAssetsManager, userLocal, out _, testName: secondTestName, callerFilePath: secondFile);

        Path.GetDirectoryName(second.dotnetRoot).Should().Be(expectedSecond.Path);
        second.dotnetRoot.Should().NotBe(first.dotnetRoot);
        File.ReadAllText(markerPath).Should().Be("first caller");
    }
}
