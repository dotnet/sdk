// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy.Tests;

/// <summary>
/// Unit Tests for <see cref="CreatePackageFile"/>.
/// </summary>
[TestClass]
public class CreatePackageFileTests
{
    private const string TestPackageExtension = ".test";
    private const string ProjectName = "TestProject";
    private const string ContentToPackage = $@"z:\Users\testUser\source\Solution\{ProjectName}";
    private const string IntermediateTempPath = $@"{ContentToPackage}\bin\net8.0\{ProjectName}";

    [TestMethod]
    [DataRow(true, TestPackageExtension, IntermediateTempPath)]
    [DataRow(false, null, null)]
    public void CreatePackageFile_Execute(bool expectedResult, string expectedFileExtension, string expectedFileDirectory)
    {
        // Arrange
        var testPackageFilePath = Path.Combine(IntermediateTempPath, "uniqueFileName");

        var filePackagerMock = new Mock<IFilePackager>();
        filePackagerMock.SetupGet(fp => fp.Extension).Returns(TestPackageExtension);
        filePackagerMock
            .Setup(fp => fp.CreatePackageAsync(ContentToPackage, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var createPackageFileTask = new CreatePackageFile(filePackagerMock.Object)
        {
            ContentToPackage = ContentToPackage,
            ProjectName = ProjectName,
            IntermediateTempPath = IntermediateTempPath,
        };

        // Act
        var result = createPackageFileTask.Execute();

        // Assert: 'CreatePackageFile' task result expected results
        Assert.AreEqual(expectedResult, result);

        if (expectedResult)
        {
            Assert.AreEqual(expectedFileDirectory, Path.GetDirectoryName(createPackageFileTask.CreatedPackageFilePath));
            Assert.AreEqual(expectedFileExtension, Path.GetExtension(createPackageFileTask.CreatedPackageFilePath));
        }
        else
        {
            Assert.IsTrue(string.IsNullOrEmpty(createPackageFileTask.CreatedPackageFilePath));
        }

        filePackagerMock.VerifyAll();
    }

    [TestMethod]
    [DataRow(null, ProjectName, IntermediateTempPath)]
    [DataRow("", ProjectName, IntermediateTempPath)]
    [DataRow(ContentToPackage, null, IntermediateTempPath)]
    [DataRow(ContentToPackage, "", IntermediateTempPath)]
    [DataRow(ContentToPackage, ProjectName, null)]
    [DataRow(ContentToPackage, ProjectName, "")]
    [DataRow("", "", "")]
    [DataRow(null, null, null)]
    public void CreatePackageFile_Execute_MissingValues(string contentToPackage, string projectName, string intermediateTempPath)
    {
        // Arrange
        var filePackagerMock = new Mock<IFilePackager>();

        var createPackageFileTask = new CreatePackageFile(filePackagerMock.Object)
        {
            ContentToPackage = contentToPackage,
            ProjectName = projectName,
            IntermediateTempPath = intermediateTempPath,
        };

        // Act
        var result = createPackageFileTask.Execute();

        // Assert: 'CreatePackageFile' task results in 'False' due to missing values
        Assert.IsFalse(result);
        Assert.IsTrue(string.IsNullOrEmpty(createPackageFileTask.CreatedPackageFilePath));
        filePackagerMock.VerifyAll();
    }

    [TestMethod]
    public void CreatePackagerFile_ZipPackager_Default()
    {
        // Act
        var createPackageFile = new CreatePackageFile();

        // Assert:
        // - Default ctor (as used by MSBuild) can correctly instantiate an instance.
        // - A 'ZipFilePackager' is set as the default 'IFilePackager' (though we can't verify that, here)
        Assert.IsNotNull(createPackageFile);
    }
}
