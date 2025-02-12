// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.LocalDaemons;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class ArchiveFileRegistryTests
{
    [Fact]
    public async Task ArchiveOutputPathIsExistingDirectory_CreatesFileWithRepositoryNameAndTarGz()
    {
        string archiveOutputPath = TestSettings.TestArtifactsDirectory;
        string expectedCreatedFilePath = Path.Combine(TestSettings.TestArtifactsDirectory, "repository.tar.gz");

        await CreateRegistryAndCallLoadAsync(archiveOutputPath);
        
        Assert.True(File.Exists(expectedCreatedFilePath));    
    }  

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchiveOutputPathIsNonExistingDirectory_CreatesDirectoryAndFileWithRepositoryNameAndTarGz(bool includeDirectorySeperatorAtTheEnd)
    {
        string archiveOutputPath = Path.Combine(
            TestSettings.TestArtifactsDirectory,
             "nonexisting" + (includeDirectorySeperatorAtTheEnd ? Path.DirectorySeparatorChar : ""));
        string expectedCreatedFilePath = Path.Combine(archiveOutputPath, "repository.tar.gz");

        await CreateRegistryAndCallLoadAsync(archiveOutputPath);
        
        Assert.True(File.Exists(expectedCreatedFilePath));    
    }

    [Fact]
    public async Task ArchiveOutputPathIsCustomFileNameInExistingDirectory_CreatesFileWithThatName()
    {
        string archiveOutputPath = Path.Combine(TestSettings.TestArtifactsDirectory, "custom-name.withextension");
        string expectedCreatedFilePath = archiveOutputPath;

        await CreateRegistryAndCallLoadAsync(archiveOutputPath);
        
        Assert.True(File.Exists(expectedCreatedFilePath));    
    }

    [Fact]
    public async Task ArchiveOutputPathIsCustomFileNameInNonExistingDirectory_CreatesDirectoryAndFileWithThatName()
    {
        string archiveOutputPath = Path.Combine(TestSettings.TestArtifactsDirectory, $"nonexisting-directory{Path.AltDirectorySeparatorChar}custom-name.withextension");
        string expectedCreatedFilePath = archiveOutputPath;

        await CreateRegistryAndCallLoadAsync(archiveOutputPath);
        
        Assert.True(File.Exists(expectedCreatedFilePath));    
    }

    private async Task CreateRegistryAndCallLoadAsync(string archiveOutputPath)
    {
        var registry = new ArchiveFileRegistry(archiveOutputPath);
        var destinationImageReference = new DestinationImageReference(registry, "repository", ["tag"]);

        await registry.LoadAsync(
            "test image",
            new SourceImageReference(),
            destinationImageReference,
            CancellationToken.None,
            async (img, srcRef, destRef, stream, token) =>
            {
                await Task.CompletedTask;
            });
    }
}