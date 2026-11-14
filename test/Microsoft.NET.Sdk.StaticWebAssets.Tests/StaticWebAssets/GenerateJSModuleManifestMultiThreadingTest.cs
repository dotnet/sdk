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
    [DataRow(false)]
    [DataRow(true, DisplayName = "Non-ASCII whitespace parent directory")]
    public void RelativeOutputsAreIsolatedByProjectAndUnchangedManifestsAreNotRewritten(bool nonAsciiWhitespace)
    {
        string firstDirectory = TestAssetsManager.CreateTestDirectory(identifier: $"{nonAsciiWhitespace}_first").Path;
        string secondDirectory = TestAssetsManager.CreateTestDirectory(identifier: $"{nonAsciiWhitespace}_second").Path;
        string decoyDirectory = TestAssetsManager.CreateTestDirectory(identifier: $"{nonAsciiWhitespace}_decoy").Path;
        string outputDirectory = nonAsciiWhitespace ? "\u00A0" : "generated";
        string outputFile = nonAsciiWhitespace
            ? Path.Combine(outputDirectory, "modules.json")
            : Path.Combine("obj", "..", outputDirectory, "modules.json");
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

            string firstFile = Path.Combine(firstDirectory, outputDirectory, "modules.json");
            string secondFile = Path.Combine(secondDirectory, outputDirectory, "modules.json");
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

            first.JsModules = [new TaskItem("changed.module.js")];
            Assert.IsTrue(first.Execute());

            Assert.AreSequenceEqual(
                new[] { "changed.module.js" },
                JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(firstFile)));
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
    [DataRow(" ", false)]
    [DataRow("\u00A0", true)]
    public void FrameworkTaskUsesTheActualMSBuildRuntimeForPathValidation(string outputDirectory, bool validPath)
    {
        string directory = TestAssetsManager.CreateTestDirectory(identifier: validPath.ToString()).Path;
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
                        new XAttribute("OutputFile", Path.Combine(outputDirectory, "modules.json")),
                        new XAttribute("JsModules", "")))))
            .Save(projectFile);

        var result = new DotnetCommand(Log, "exec", Path.Combine(toolset.SdkFolderUnderTest, "MSBuild.dll"),
            projectFile, "-t:Validate", "-mt", "-nr:false", BinLogArgument(["FrameworkTaskOnCoreRuntime"]))
            .WithWorkingDirectory(directory)
            .Execute();

        if (validPath)
        {
            result.Should().Pass();
            Assert.IsEmpty(JsonSerializer.Deserialize<string[]>(
                File.ReadAllBytes(Path.Combine(directory, outputDirectory, "modules.json")))!);
        }
        else
        {
            result.Should().Fail()
                .And.HaveStdOutContaining("System.ArgumentException")
                .And.NotHaveStdOutContaining("System.IO.DirectoryNotFoundException");
        }
    }
}
