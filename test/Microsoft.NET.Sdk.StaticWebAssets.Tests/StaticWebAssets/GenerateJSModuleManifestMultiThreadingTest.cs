// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework.Commands;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public sealed class GenerateJSModuleManifestMultiThreadingTest : SdkTest
{
    [TestMethod]
    // Other tests can resolve relative paths without acquiring a CurrentDirectory resource lock.
    [DoNotParallelize]
    public void RelativeOutputsAreIsolatedByProjectAndUnchangedManifestsAreNotRewritten()
    {
        string firstDirectory = TestAssetsManager.CreateTestDirectory(identifier: "first").Path;
        string secondDirectory = TestAssetsManager.CreateTestDirectory(identifier: "second").Path;
        string decoyDirectory = TestAssetsManager.CreateTestDirectory(identifier: "decoy").Path;
        string outputFile = Path.Combine("obj", "..", "generated", "modules.json");
        string originalDirectory = Directory.GetCurrentDirectory();
        var firstEngine = new Mock<IBuildEngine>();

        try
        {
            Directory.SetCurrentDirectory(decoyDirectory);
            var first = new GenerateJSModuleManifest
            {
                BuildEngine = firstEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(firstDirectory),
                OutputFile = outputFile,
                JsModules = [new TaskItem("z.module.js"), new TaskItem("a.module.js")],
            };
            var second = new GenerateJSModuleManifest
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(secondDirectory),
                OutputFile = outputFile,
                JsModules = [new TaskItem("second.module.js")],
            };

            Parallel.Invoke(
                () => Assert.IsTrue(first.Execute()),
                () => Assert.IsTrue(second.Execute()));

            string firstFile = Path.Combine(firstDirectory, "generated", "modules.json");
            string secondFile = Path.Combine(secondDirectory, "generated", "modules.json");
            Assert.AreSequenceEqual(
                new[] { "a.module.js", "z.module.js" },
                JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(firstFile)));
            Assert.AreSequenceEqual(
                new[] { "second.module.js" },
                JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(secondFile)));

            byte[] content = File.ReadAllBytes(firstFile);
            var timestamp = new DateTime(2001, 2, 3, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(firstFile, timestamp);

            Assert.IsTrue(first.Execute());

            Assert.AreSequenceEqual(content, File.ReadAllBytes(firstFile));
            Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(firstFile));
            Assert.AreEqual(outputFile, first.OutputFile);
            Assert.AreEqual(outputFile, second.OutputFile);
            Assert.IsEmpty(Directory.GetFileSystemEntries(decoyDirectory));
            firstEngine.Verify(engine => engine.LogMessageEvent(It.Is<BuildMessageEventArgs>(
                message => message.Message == $"Creating manifest because manifest file '{outputFile}' does not exist.")),
                Times.Once);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [TestMethod]
    // The legacy validation probe intentionally uses process-relative invalid directories.
    [DoNotParallelize]
    [DataRow(null, typeof(ArgumentNullException))]
    [DataRow("", typeof(ArgumentNullException))]
    [DataRow("modules.json", typeof(ArgumentException))]
    [DataRow(" ", typeof(ArgumentNullException))]
    public void OutputWithoutADirectoryPreservesItsException(string? outputFile, Type expectedType)
    {
        var task = new GenerateJSModuleManifest
        {
            BuildEngine = Mock.Of<IBuildEngine>(),
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(
                TestAssetsManager.CreateTestDirectory(identifier: outputFile ?? "null").Path),
            OutputFile = outputFile!,
            JsModules = [],
        };

        var expected = Assert.Throws<ArgumentException>(() => Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!));
        var exception = Assert.Throws<ArgumentException>(() => task.Execute());

        if (outputFile == " " && !OperatingSystem.IsWindows())
        {
            expectedType = typeof(ArgumentException);
        }

        Assert.AreEqual(expectedType, exception.GetType());
        Assert.AreEqual("path", exception.ParamName);
        Assert.AreEqual(expected.Message, exception.Message);
    }

    [TestMethod]
    // The legacy validation probe intentionally uses a process-relative invalid directory.
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public void WhitespaceParentPreservesItsDirectoryValidationException()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        var task = new GenerateJSModuleManifest
        {
            BuildEngine = Mock.Of<IBuildEngine>(),
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(directory),
            OutputFile = Path.Combine(" ", "modules.json"),
            JsModules = [],
        };
        var expected = Assert.Throws<ArgumentException>(() => Directory.CreateDirectory(" "));

        var actual = Assert.Throws<ArgumentException>(() => task.Execute());

        Assert.AreEqual(expected.GetType(), actual.GetType());
        Assert.AreEqual(expected.Message, actual.Message);
        Assert.IsEmpty(Directory.GetFileSystemEntries(directory));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void FrameworkTaskUsesTheActualMSBuildRuntimeForPathValidation()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        var toolset = SdkTestContext.Current.ToolsetUnderTest;
        string taskAssembly = Path.Combine(toolset.SdksPath,
            "Microsoft.NET.Sdk.StaticWebAssets", "tasks", "net472", "Microsoft.NET.Sdk.StaticWebAssets.Tasks.dll");
        string projectFile = Path.Combine(directory, "Validate.proj");
        new XDocument(
            new XElement("Project",
                new XElement("UsingTask",
                    new XAttribute("TaskName", "Microsoft.AspNetCore.StaticWebAssets.Tasks.GenerateJSModuleManifest"),
                    new XAttribute("AssemblyFile", taskAssembly)),
                new XElement("Target", new XAttribute("Name", "Validate"),
                    new XElement("GenerateJSModuleManifest",
                        new XAttribute("OutputFile", Path.Combine(" ", "modules.json")),
                        new XAttribute("JsModules", "")))))
            .Save(projectFile);

        new DotnetCommand(Log, "exec", Path.Combine(toolset.SdkFolderUnderTest, "MSBuild.dll"),
            projectFile, "-t:Validate", "-mt", "-nr:false", BinLogArgument(["FrameworkTaskOnCoreRuntime"]))
            .WithWorkingDirectory(directory)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("System.ArgumentException")
            .And.NotHaveStdOutContaining("System.IO.DirectoryNotFoundException");
    }
}
