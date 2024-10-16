// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy.Tests;

/// <summary>
/// Unit Tests for <see cref="CreatePackageFile"/>.
/// </summary>
public class CreatePackageFileTests
{
    private const string TestPackageExtension = ".test";
    private const string ProjectName = "TestProject";
    private const string ContentToPackage = $@"z:\Users\testUser\source\Solution\{ProjectName}";
    private const string IntermediateTempPath = $@"{ContentToPackage}\bin\net8.0\{ProjectName}";

    [Theory]
    [InlineData(true, TestPackageExtension, IntermediateTempPath)]
    [InlineData(false, null, null)]
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
        Assert.Equal(expectedResult, result);

        if (expectedResult)
        {
            Assert.Equal(expectedFileDirectory, Path.GetDirectoryName(createPackageFileTask.CreatedPackageFilePath));
            Assert.Equal(expectedFileExtension, Path.GetExtension(createPackageFileTask.CreatedPackageFilePath));
        }
        else
        {
            Assert.True(string.IsNullOrEmpty(createPackageFileTask.CreatedPackageFilePath));
        }

        filePackagerMock.VerifyAll();
    }

    [Theory]
    [InlineData(null, ProjectName, IntermediateTempPath)]
    [InlineData("", ProjectName, IntermediateTempPath)]
    [InlineData(ContentToPackage, null, IntermediateTempPath)]
    [InlineData(ContentToPackage, "", IntermediateTempPath)]
    [InlineData(ContentToPackage, ProjectName, null)]
    [InlineData(ContentToPackage, ProjectName, "")]
    [InlineData("", "", "")]
    [InlineData(null, null, null)]
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
        Assert.False(result);
        Assert.True(string.IsNullOrEmpty(createPackageFileTask.CreatedPackageFilePath));
        filePackagerMock.VerifyAll();
    }

    [Fact]
    public void CreatePackagerFile_ZipPackager_Default()
    {
        // Act
        var createPackageFile = new CreatePackageFile();

        // Assert:
        // - Default ctor (as used by MSBuild) can correctly instantiate an instance.
        // - A 'ZipFilePackager' is set as the default 'IFilePackager' (though we can't verify that, here)
        Assert.True(createPackageFile is not null);
    }
}
