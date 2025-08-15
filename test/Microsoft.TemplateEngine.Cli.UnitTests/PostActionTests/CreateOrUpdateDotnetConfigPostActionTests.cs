// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

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
        string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

        IPostAction postAction = CreatePostActionForMTP();

        Mock<IReporter> mockReporter = new();

        mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
            .Verifiable();

        Reporter.SetOutput(mockReporter.Object);

        CreateOrUpdateDotnetConfigPostActionProcessor processor = new();

        _engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetBasePath, "dotnet.config")).Should().BeFalse();

        bool result = processor.Process(
            _engineEnvironmentSettings,
            postAction,
            new MockCreationEffects(),
            new MockCreationResult(),
            targetBasePath);

        Assert.True(result);

        _engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetBasePath, "dotnet.config")).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(Path.Combine(targetBasePath, "dotnet.config")).Should().Be("""
            [dotnet.test.runner]
            name = "Microsoft.Testing.Platform"

            """);

        mockReporter.Verify(r => r.WriteLine(LocalizableStrings.PostAction_CreateDotnetConfig_Succeeded), Times.Once);
    }

    [Fact]
    public void CreatesNewSectionWhenFileExistsButSectionDoesNot()
    {
        string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

        IPostAction postAction = CreatePostActionForMTP();
        CreateDotnetConfig(targetBasePath, "mysection", "mykey", "myvalue");
        Mock<IReporter> mockReporter = new();

        mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
            .Verifiable();

        Reporter.SetOutput(mockReporter.Object);

        CreateOrUpdateDotnetConfigPostActionProcessor processor = new();

        _engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetBasePath, "dotnet.config")).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(Path.Combine(targetBasePath, "dotnet.config")).Should().Be("""
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

        _engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetBasePath, "dotnet.config")).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(Path.Combine(targetBasePath, "dotnet.config")).Should().Be("""
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
        string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();

        IPostAction postAction = CreatePostActionForMTP();
        CreateDotnetConfig(targetBasePath, "dotnet.test.runner", "name", "Microsoft.Testing.Platform");
        Mock<IReporter> mockReporter = new();

        mockReporter.Setup(r => r.WriteLine(It.IsAny<string>()))
            .Verifiable();

        Reporter.SetOutput(mockReporter.Object);

        CreateOrUpdateDotnetConfigPostActionProcessor processor = new();

        _engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetBasePath, "dotnet.config")).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(Path.Combine(targetBasePath, "dotnet.config")).Should().Be("""
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

        _engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(targetBasePath, "dotnet.config")).Should().BeTrue();
        _engineEnvironmentSettings.Host.FileSystem.ReadAllText(Path.Combine(targetBasePath, "dotnet.config")).Should().Be("""
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

    private void CreateDotnetConfig(string targetBasePath, string section, string key, string value)
        => _engineEnvironmentSettings.Host.FileSystem.WriteAllText(Path.Combine(targetBasePath, "dotnet.config"), $"""
            [{section}]
            {key} = "{value}"

            """);
}
