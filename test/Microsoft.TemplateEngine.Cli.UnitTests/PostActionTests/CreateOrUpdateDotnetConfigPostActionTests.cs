// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

using Moq;

namespace Microsoft.TemplateEngine.Cli.UnitTests.PostActionTests;

public class CreateOrUpdateDotnetConfigPostActionTests : IClassFixture<EnvironmentSettingsHelper>
{
    private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

    public CreateOrUpdateDotnetConfigPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
    {
        _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
    }

    [Theory]
    [InlineData("section")]
    [InlineData("key")]
    [InlineData("value")]
    public void MissingArgumentShouldFail(string missingArgumentName)
    {
        string targetBasePath = GetTargetPath();

        var dictionary = new Dictionary<string, string>
        {
            ["section"] = "dotnet.test.runner",
            ["key"] = "name",
            ["value"] = "Microsoft.Testing.Platform"
        };
        dictionary.Remove(missingArgumentName);

        IPostAction postAction = new MockPostAction(default, default, default, default, default!)
        {
            ActionId = CreateOrUpdateDotnetConfigPostActionProcessor.ActionProcessorId,
            Args = dictionary,
        };

        Mock<IReporter> mockReporter = new();

        mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
            .Verifiable();

        Reporter.SetError(mockReporter.Object);

        CreateOrUpdateDotnetConfigPostActionProcessor processor = new();

        bool result = processor.Process(
            _engineEnvironmentSettings,
            postAction,
            new MockCreationEffects(),
            new MockCreationResult(),
            targetBasePath);

        Assert.False(result);

        mockReporter.Verify(r => r.WriteLine(string.Format(LocalizableStrings.PostAction_DotnetConfig_Error_ArgumentNotConfigured, missingArgumentName)), Times.Once);
    }

    [Fact]
    public void CreatesDotnetConfigWhenDoesNotExist()
    {
        string targetBasePath = GetTargetPath();
        string dotnetConfigPath = Path.Combine(targetBasePath, "dotnet.config");

        IPostAction postAction = CreatePostActionForMTP();

        Mock<IReporter> mockReporter = new();

        mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
            .Verifiable();

        Reporter.SetOutput(mockReporter.Object);

        CreateOrUpdateDotnetConfigPostActionProcessor processor = new();

        _engineEnvironmentSettings.Host.FileSystem.FileExists(dotnetConfigPath).Should().BeFalse();

        bool result = processor.Process(
            _engineEnvironmentSettings,
            postAction,
            new MockCreationEffects(),
            new MockCreationResult(),
            targetBasePath);

        Assert.True(result);

        _engineEnvironmentSettings.Host.FileSystem.FileExists(dotnetConfigPath).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(dotnetConfigPath).Should().Be("""
            [dotnet.test.runner]
            name = "Microsoft.Testing.Platform"

            """);

        mockReporter.Verify(r => r.WriteLine(LocalizableStrings.PostAction_CreateDotnetConfig_Succeeded), Times.Once);
    }

    [Fact]
    public void CreatesNewSectionWhenFileExistsButSectionDoesNot()
    {
        string targetBasePath = GetTargetPath();
        string dotnetConfigPath = Path.Combine(targetBasePath, "dotnet.config");

        IPostAction postAction = CreatePostActionForMTP();
        CreateDotnetConfig(dotnetConfigPath, "mysection", "mykey", "myvalue");
        Mock<IReporter> mockReporter = new();

        mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
            .Verifiable();

        Reporter.SetOutput(mockReporter.Object);

        CreateOrUpdateDotnetConfigPostActionProcessor processor = new();

        _engineEnvironmentSettings.Host.FileSystem.FileExists(dotnetConfigPath).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(dotnetConfigPath).Should().Be("""
            [mysection]
            mykey = "myvalue"

            """);

        bool result = processor.Process(
            _engineEnvironmentSettings,
            postAction,
            new MockCreationEffects(),
            new MockCreationResult(),
            targetBasePath);

        Assert.True(result);

        _engineEnvironmentSettings.Host.FileSystem.FileExists(dotnetConfigPath).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(dotnetConfigPath).Should().Be("""
            [mysection]
            mykey = "myvalue"


            [dotnet.test.runner]
            name = "Microsoft.Testing.Platform"

            """);

        mockReporter.Verify(r => r.WriteLine(LocalizableStrings.PostAction_CreateDotnetConfig_CreatedNewSection), Times.Once);
    }

    [Fact]
    public void DoesNothingIfNoUpdatesNeedToHappen()
    {
        string targetBasePath = GetTargetPath();
        string dotnetConfigPath = Path.Combine(targetBasePath, "dotnet.config");

        IPostAction postAction = CreatePostActionForMTP();
        CreateDotnetConfig(dotnetConfigPath, "dotnet.test.runner", "name", "Microsoft.Testing.Platform");
        Mock<IReporter> mockReporter = new();

        mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
            .Verifiable();

        Reporter.SetOutput(mockReporter.Object);

        CreateOrUpdateDotnetConfigPostActionProcessor processor = new();

        _engineEnvironmentSettings.Host.FileSystem.FileExists(dotnetConfigPath).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(dotnetConfigPath).Should().Be("""
            [dotnet.test.runner]
            name = "Microsoft.Testing.Platform"

            """);

        bool result = processor.Process(
            _engineEnvironmentSettings,
            postAction,
            new MockCreationEffects(),
            new MockCreationResult(),
            targetBasePath);

        Assert.True(result);

        _engineEnvironmentSettings.Host.FileSystem.FileExists(dotnetConfigPath).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(dotnetConfigPath).Should().Be("""
            [dotnet.test.runner]
            name = "Microsoft.Testing.Platform"

            """);

        mockReporter.Verify(r => r.WriteLine(LocalizableStrings.PostAction_CreateDotnetConfig_ValueAlreadyExist), Times.Once);
    }

    private static IPostAction CreatePostActionForMTP()
        => new MockPostAction(default, default, default, default, default!)
        {
            ActionId = CreateOrUpdateDotnetConfigPostActionProcessor.ActionProcessorId,
            Args = new Dictionary<string, string>
            {
                ["section"] = "dotnet.test.runner",
                ["key"] = "name",
                ["value"] = "Microsoft.Testing.Platform",
            },
        };

    private void CreateDotnetConfig(string dotnetConfigPath, string section, string key, string value)
        => _engineEnvironmentSettings.Host.FileSystem.WriteAllText(dotnetConfigPath, $"""
            [{section}]
            {key} = "{value}"

            """);

    private string GetTargetPath([CallerMemberName] string testName = "")
    {
        string targetBasePath = Path.Combine(_engineEnvironmentSettings.GetTempVirtualizedPath(), testName);
        _engineEnvironmentSettings.Host.FileSystem.CreateDirectory(targetBasePath);

        // This is done to not let the preprocessor logic go above our base path directory.
        _engineEnvironmentSettings.Host.FileSystem.CreateDirectory(Path.Combine(targetBasePath, ".git"));
        return targetBasePath;
    }
}
