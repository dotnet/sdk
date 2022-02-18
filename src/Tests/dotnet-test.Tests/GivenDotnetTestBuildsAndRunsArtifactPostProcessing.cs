// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsArtifactPostProcessing : SdkTest
    {
        private static object s_dataCollectorInitLock = new();
        private static string s_dataCollectorDll;
        private static string s_dataCollectorNoMergeDll;

        public GivenDotnetTestBuildsAndRunsArtifactPostProcessing(ITestOutputHelper log) : base(log)
        {
            BuildDataCollector();
            BuildDataCollectorNoMerge();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ArtifactPostProcessing_CsProjs(bool merge)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("VSTestMultiProjectSolution")
                .WithSource();

            string runsettings = GetRunsetting(testInstance.Path);

            CommandResult result = new DotnetTestCommand(Log)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnvironmentVariable(FeatureFlag.ARTIFACTS_POSTPROCESSING, "1")
                                    .Execute(
                                    "--configuration", "release",
                                    "--collect", "SampleDataCollector",
                                    "--test-adapter-path", merge ? Path.GetDirectoryName(s_dataCollectorDll) : Path.GetDirectoryName(s_dataCollectorNoMergeDll),
                                    "--settings", runsettings,
                                    "--diag", testInstance.Path + "/logs/");

            result.ExitCode.Should().Be(0);
            AssertOutput(result.StdOut, merge);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ArtifactPostProcessing_TestContainers(bool merge)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("VSTestMultiProjectSolution")
                .WithSource();

            string runsettings = GetRunsetting(testInstance.Path);

            new PublishCommand(Log, Path.Combine(testInstance.Path, "sln.sln")).Execute("/p:Configuration=Release").Should().Pass();

            CommandResult result = new DotnetTestCommand(Log)
                                    .WithWorkingDirectory(testInstance.Path)
                                    .WithEnvironmentVariable(FeatureFlag.ARTIFACTS_POSTPROCESSING, "1")
                                    .WithEnvironmentVariable("DOTNET_CLI_VSTEST_TRACE", "1")
                                    .Execute(
                                    Directory.GetFiles(testInstance.Path, "test1.dll", SearchOption.AllDirectories).SingleOrDefault(x => x.Contains("publish")),
                                    Directory.GetFiles(testInstance.Path, "test2.dll", SearchOption.AllDirectories).SingleOrDefault(x => x.Contains("publish")),
                                    Directory.GetFiles(testInstance.Path, "test3.dll", SearchOption.AllDirectories).SingleOrDefault(x => x.Contains("publish")),
                                    "--collect:SampleDataCollector",
                                    $"--test-adapter-path:{(merge ? Path.GetDirectoryName(s_dataCollectorDll) : Path.GetDirectoryName(s_dataCollectorNoMergeDll))}",
                                    $"--settings:{runsettings}",
                                    "--diag:" + testInstance.Path + "/logs/");

            result.ExitCode.Should().Be(0);
            AssertOutput(result.StdOut, merge);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ArtifactPostProcessing_VSTest_TestContainers(bool merge)
        {
            TestAsset testInstance = _testAssetsManager.CopyTestAsset("VSTestMultiProjectSolution")
                .WithSource();

            string runsettings = GetRunsetting(testInstance.Path);

            new PublishCommand(Log, Path.Combine(testInstance.Path, "sln.sln")).Execute("/p:Configuration=Release").Should().Pass();

            CommandResult result = new DotnetVSTestCommand(Log)
              .WithWorkingDirectory(testInstance.Path)
                                    .WithEnvironmentVariable(FeatureFlag.ARTIFACTS_POSTPROCESSING, "1")
                                    .Execute(
                                    Directory.GetFiles(testInstance.Path, "test1.dll", SearchOption.AllDirectories).SingleOrDefault(x => x.Contains("publish")),
                                    Directory.GetFiles(testInstance.Path, "test2.dll", SearchOption.AllDirectories).SingleOrDefault(x => x.Contains("publish")),
                                    Directory.GetFiles(testInstance.Path, "test3.dll", SearchOption.AllDirectories).SingleOrDefault(x => x.Contains("publish")),
                                    "--collect:SampleDataCollector",
                                    $"--testAdapterPath:{(merge ? Path.GetDirectoryName(s_dataCollectorDll) : Path.GetDirectoryName(s_dataCollectorNoMergeDll))}",
                                    $"--settings:{runsettings}",
                                    "--diag:" + testInstance.Path + "/logs/");

            result.ExitCode.Should().Be(0);
            AssertOutput(result.StdOut, merge);
        }

        private static void AssertOutput(string stdOut, bool merge)
        {
            List<string> output = new();
            using StringReader reader = new(stdOut);
            while (true)
            {
                string line = reader.ReadLine()?.Trim();
                if (line is null) break;
                output.Add(line);
            }

            if (merge)
            {
                Assert.Equal(string.Empty, output[output.Count - 3].Trim());
                Assert.Equal("Attachments:", output[output.Count - 2].Trim());
                string mergedFile = output[output.Count - 1].Trim();

                var fileContent = new List<string>();
                using var streamReader = new StreamReader(mergedFile);
                while (!streamReader.EndOfStream)
                {
                    string line = streamReader.ReadLine();
                    Assert.StartsWith("SessionEnded_Handler_", line);
                    fileContent.Add(line);
                }

                Assert.Equal(3, fileContent.Distinct().Count());
            }
            else
            {
                Assert.Equal(string.Empty, output[output.Count - 5].Trim());
                Assert.Equal("Attachments:", output[output.Count - 4].Trim());

                int currentLine = 0;
                for (int i = 3; i > 0; i--)
                {
                    currentLine = output.Count - i;
                    string file = output[currentLine].Trim();
                    var fileContent = new List<string>();
                    using var streamReader = new StreamReader(file);
                    while (!streamReader.EndOfStream)
                    {
                        string line = streamReader.ReadLine();
                        Assert.StartsWith("SessionEnded_Handler_", line);
                        fileContent.Add(line);
                    }

                    Assert.True(fileContent.Distinct().Count() == 1);
                }

                Assert.Equal(output.Count, currentLine + 1);
            }
        }

        private void BuildDataCollector()
            => LazyInitializer.EnsureInitialized(ref s_dataCollectorDll, ref s_dataCollectorInitLock, () =>
                {
                    TestAsset testInstance = _testAssetsManager.CopyTestAsset("VSTestDataCollectorSample").WithSource();

                    string testProjectDirectory = testInstance.Path;

                    new BuildCommand(testInstance)
                        .Execute("/p:Configuration=Release")
                        .Should()
                        .Pass();

                    return Directory.GetFiles(testProjectDirectory, "AttachmentProcessorDataCollector.dll", SearchOption.AllDirectories).Single(x => x.Contains("bin"));
                });

        private void BuildDataCollectorNoMerge()
            => LazyInitializer.EnsureInitialized(ref s_dataCollectorNoMergeDll, ref s_dataCollectorInitLock, () =>
            {
                TestAsset testInstance = _testAssetsManager.CopyTestAsset("VSTestDataCollectorSampleNoMerge").WithSource();

                string testProjectDirectory = testInstance.Path;

                new BuildCommand(testInstance)
                            .Execute("/p:Configuration=Release")
                            .Should()
                            .Pass();

                return Directory.GetFiles(testProjectDirectory, "AttachmentProcessorDataCollector.dll", SearchOption.AllDirectories).Single(x => x.Contains("bin"));
            });

        private static string GetRunsetting(string directory)
        {
            string runSettings = GetRunsettingsFilePath(directory);
            // Set datacollector parameters
            XElement runSettingsXml = XElement.Load(runSettings);
            runSettingsXml.Element("DataCollectionRunSettings")
                .Element("DataCollectors")
                .Element("DataCollector")
                .Add(new XElement("Configuration", new XElement("MergeFile", "MergedFile.txt")));
            runSettingsXml.Save(runSettings);
            return runSettings;
        }

        private static string GetRunsettingsFilePath(string resultsDir)
        {
            string runsettingsPath = Path.Combine(resultsDir, "test_" + Guid.NewGuid() + ".runsettings");
            var dataCollectionAttributes = new Dictionary<string, string>
            {
                { "friendlyName", "SampleDataCollector" },
                { "uri", "my://sample/datacollector" }
            };

            CreateDataCollectionRunSettingsFile(runsettingsPath, dataCollectionAttributes);
            return runsettingsPath;
        }

        private static void CreateDataCollectionRunSettingsFile(string destinationRunsettingsPath, Dictionary<string, string> dataCollectionAttributes)
        {
            var doc = new XmlDocument();
            XmlNode xmlDeclaration = doc.CreateNode(XmlNodeType.XmlDeclaration, string.Empty, string.Empty);

            doc.AppendChild(xmlDeclaration);
            XmlElement runSettingsNode = doc.CreateElement("RunSettings");
            doc.AppendChild(runSettingsNode);
            XmlElement dcConfigNode = doc.CreateElement("DataCollectionRunSettings");
            runSettingsNode.AppendChild(dcConfigNode);
            XmlElement dataCollectorsNode = doc.CreateElement("DataCollectors");
            dcConfigNode.AppendChild(dataCollectorsNode);
            XmlElement dataCollectorNode = doc.CreateElement("DataCollector");
            dataCollectorsNode.AppendChild(dataCollectorNode);

            foreach (KeyValuePair<string, string> kvp in dataCollectionAttributes)
            {
                dataCollectorNode.SetAttribute(kvp.Key, kvp.Value);
            }

            using var stream = new FileStream(destinationRunsettingsPath, FileMode.Create);
            doc.Save(stream);
        }
    }
}
