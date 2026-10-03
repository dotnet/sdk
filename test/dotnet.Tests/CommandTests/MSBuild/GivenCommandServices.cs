// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Tool.Store;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Moq;
using BuildCommand = Microsoft.DotNet.Cli.Commands.Build.BuildCommand;
using CleanCommand = Microsoft.DotNet.Cli.Commands.Clean.CleanCommand;
using MSBuildCommand = Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildCommand;
using PackCommand = Microsoft.DotNet.Cli.Commands.Pack.PackCommand;
using PublishCommand = Microsoft.DotNet.Cli.Commands.Publish.PublishCommand;
using RestoreCommand = Microsoft.DotNet.Cli.Commands.Restore.RestoreCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

[TestClass]
public class GivenCommandServices : SdkTest
{
    [TestMethod]
    [DataRow("build")]
    [DataRow("clean")]
    [DataRow("msbuild")]
    [DataRow("pack")]
    [DataRow("publish")]
    [DataRow("restore")]
    [DataRow("store")]
    public void LLMDetectionIsPerCommand(string commandName)
    {
        var llmServices = CreateServices(isLLMEnvironment: true);
        var nonLLMServices = CreateServices(isLLMEnvironment: false);

        var llmCommand = (MSBuildForwardingApp)CreateCommand(commandName, llmServices);
        var nonLLMCommand = (MSBuildForwardingApp)CreateCommand(commandName, nonLLMServices);
        var defaultCommand = CreateCommand(commandName);

        llmCommand.Services.Should().BeSameAs(llmServices);
        nonLLMCommand.Services.Should().BeSameAs(nonLLMServices);
        defaultCommand.Services.LLMEnvironmentDetector.Should().BeOfType<LLMEnvironmentDetectorForTelemetry>();
        AssertLLMArguments(llmCommand, expected: true);
        AssertLLMArguments(nonLLMCommand, expected: false);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SeparateRestoreSharesCommandServices(bool isLLMEnvironment)
    {
        var services = CreateServices(isLLMEnvironment);
        var command = (RestoringCommand)BuildCommand.FromArgs(
            ["-p:TargetFramework=tfm"],
            msbuildPath: "<msbuildpath>",
            services: services);

        command.SeparateRestoreCommand.Should().NotBeNull();
        command.SeparateRestoreCommand!.Services.Should().BeSameAs(command.Services).And.BeSameAs(services);
        AssertLLMArguments(command, isLLMEnvironment);
        AssertLLMArguments(command.SeparateRestoreCommand, isLLMEnvironment);
    }

    [TestMethod]
    [DataRow("build")]
    [DataRow("clean")]
    [DataRow("pack")]
    [DataRow("publish")]
    [DataRow("restore")]
    public void FileBasedCommandUsesItsServicesWhenCreatingLogger(string commandName)
    {
        var directory = TestAssetsManager.CreateTestDirectory(identifier: commandName);
        var entryPoint = Path.Combine(directory.Path, "Program.cs");
        File.WriteAllText(entryPoint, "Console.WriteLine(\"Hello\");");

        // Stop at logger creation, before in-process MSBuild can change shared process state.
        var stopBeforeBuild = new InvalidOperationException("Stop before MSBuild starts.");
        var detector = new Mock<ILLMEnvironmentDetector>(MockBehavior.Strict);
        detector.Setup(d => d.IsLLMEnvironment()).Throws(stopBeforeBuild);
        var services = new CommandServices(detector.Object);
        var command = (VirtualProjectBuildingCommand)CreateCommand(commandName, services, [entryPoint]);

        command.Services.Should().BeSameAs(services);
        detector.Verify(d => d.IsLLMEnvironment(), Times.Never);
        Action execute = () => command.Execute();
        execute.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(stopBeforeBuild);
        detector.Verify(d => d.IsLLMEnvironment(), Times.Once);
    }

    private static CommandServices CreateServices(bool isLLMEnvironment)
    {
        var detector = new Mock<ILLMEnvironmentDetector>(MockBehavior.Strict);
        detector.Setup(d => d.IsLLMEnvironment()).Returns(isLLMEnvironment);
        return new CommandServices(detector.Object);
    }

    private static CommandBase CreateCommand(string commandName, CommandServices? services = null, string[]? args = null)
    {
        args ??= [];
        const string msbuildPath = "<msbuildpath>";
        return commandName switch
        {
            "build" => BuildCommand.FromArgs(args, msbuildPath, services),
            "clean" => CleanCommand.FromArgs(args, msbuildPath, services),
            "msbuild" => MSBuildCommand.FromArgs(args, msbuildPath, services),
            "pack" => PackCommand.FromArgs(args, msbuildPath, services),
            "publish" => PublishCommand.FromArgs(args, msbuildPath, services),
            "restore" => RestoreCommand.FromArgs(args, msbuildPath, services),
            "store" => StoreCommand.FromArgs(["--manifest", "manifest.xml", .. args], msbuildPath, services),
            _ => throw new ArgumentOutOfRangeException(nameof(commandName), commandName, "Unknown command.")
        };
    }

    private static void AssertLLMArguments(MSBuildForwardingApp command, bool expected)
    {
        command.MSBuildArguments.Contains(Constants.TerminalLogger_DisableNodeDisplay).Should().Be(expected);
        command.GetArgumentTokensToMSBuild().Should().BeEquivalentTo(command.MSBuildArguments);
        command.GetProcessStartInfo().Arguments.Contains(Constants.TerminalLogger_DisableNodeDisplay).Should().Be(expected);
    }
}
