// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.BlazorWebAssembly;
using FluentAssertions;
using Moq;
using Xunit;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class GenerateBlazorWebAssemblyBootJsonTest
    {
        [Fact]
        public void GroupsResourcesByType()
        {
            // Arrange
            var taskInstance = new GenerateBlazorWebAssemblyBootJson
            {
                AssemblyPath = "MyApp.Entrypoint.dll",
                Resources = new[]
                {
                    CreateResourceTaskItem(
                        ("FileName", "My.Assembly1"),
                        ("Extension", ".dll"),
                        ("FileHash", "abcdefghikjlmnopqrstuvwxyz"),
                        ("RelativePath", "_framework/My.Assembly1.dll"),
                        ("AssetTraitName", "BlazorWebAssemblyResource"),
                        ("AssetTraitValue", "runtime")),

                    CreateResourceTaskItem(
                        ("FileName", "My.Assembly2"),
                        ("Extension", ".dll"),
                        ("FileHash", "012345678901234567890123456789"),
                        ("RelativePath", "_framework/My.Assembly2.dll"),
                        ("AssetTraitName", "BlazorWebAssemblyResource"),
                        ("AssetTraitValue", "runtime")),

                    CreateResourceTaskItem(
                        ("FileName", "SomePdb"),
                        ("Extension", ".pdb"),
                        ("FileHash", "pdbhashpdbhashpdbhash"),
                        ("RelativePath", "_framework/SomePdb.pdb"),
                        ("AssetTraitName", "BlazorWebAssemblyResource"),
                        ("AssetTraitValue", "symbol")),

                    CreateResourceTaskItem(
                        ("FileName", "My.Assembly1"),
                        ("Extension", ".pdb"),
                        ("FileHash", "pdbdefghikjlmnopqrstuvwxyz"),
                        ("RelativePath", "_framework/My.Assembly1.pdb"),
                        ("AssetTraitName", "BlazorWebAssemblyResource"),
                        ("AssetTraitValue", "symbol")),

                    CreateResourceTaskItem(
                        ("FileName", "some-runtime-file"),
                        ("RelativePath", "some-runtime-file"),
                        ("FileHash", "runtimehashruntimehash"),
                        ("AssetTraitName", "BlazorWebAssemblyResource"),
                        ("AssetTraitValue", "native")),

                    CreateResourceTaskItem(
                        ("FileName", "satellite-assembly1"),
                        ("Extension", ".dll"),
                        ("FileHash", "hashsatelliteassembly1"),
                        ("RelativePath", "satellite-assembly1.dll"),
                        ("AssetTraitName", "Culture"),
                        ("AssetTraitValue", "en-GB")),

                    CreateResourceTaskItem(
                        ("FileName", "satellite-assembly2"),
                        ("Extension", ".dll"),
                        ("FileHash", "hashsatelliteassembly2"),
                        ("RelativePath", "satellite-assembly2.dll"),
                        ("AssetTraitName", "Culture"),
                        ("AssetTraitValue", "fr")),

                    CreateResourceTaskItem(
                        ("FileName", "satellite-assembly3"),
                        ("Extension", ".dll"),
                        ("FileHash", "hashsatelliteassembly3"),
                        ("RelativePath", "satellite-assembly3.dll"),
                        ("AssetTraitName", "Culture"),
                        ("AssetTraitValue", "en-GB")),
                }
            };

            taskInstance.BuildEngine = Mock.Of<IBuildEngine>();

            using var stream = new MemoryStream();

            // Act
            taskInstance.WriteBootJson(stream, "MyEntrypointAssembly");

            // Assert
            var parsedContent = ParseBootData(stream);
            Assert.Equal("MyEntrypointAssembly", parsedContent.entryAssembly);
            parsedContent.entryAssembly.Should().Be("MyEntrypointAssembly");

            var resources = parsedContent.resources.assembly;
            resources.Count.Should().Be(2);

            resources["My.Assembly1.dll"].Should().Be("sha256-abcdefghikjlmnopqrstuvwxyz");
            resources["My.Assembly2.dll"].Should().Be("sha256-012345678901234567890123456789");

            resources = parsedContent.resources.pdb;
            resources.Count.Should().Be(2);
            resources["SomePdb.pdb"].Should().Be("sha256-pdbhashpdbhashpdbhash");
            resources["My.Assembly1.pdb"].Should().Be("sha256-pdbdefghikjlmnopqrstuvwxyz");

            resources = parsedContent.resources.runtime;
            Assert.Single(resources);
            resources.Should().HaveCount(1);
            resources["some-runtime-file"].Should().Be("sha256-runtimehashruntimehash");

            var satelliteResources = parsedContent.resources.satelliteResources;

            satelliteResources.Should().ContainKey("en-GB");
            satelliteResources["en-GB"].Should().Contain("en-GB/satellite-assembly1.dll", "sha256-hashsatelliteassembly1");
            satelliteResources["en-GB"].Should().Contain("en-GB/satellite-assembly3.dll", "sha256-hashsatelliteassembly3");


            satelliteResources.Should().ContainKey("fr");
            satelliteResources["fr"].Should().Contain("fr/satellite-assembly2.dll", "sha256-hashsatelliteassembly2");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanSpecifyCacheBootResources(bool flagValue)
        {
            // Arrange
            var taskInstance = new GenerateBlazorWebAssemblyBootJson { CacheBootResources = flagValue };
            taskInstance.BuildEngine = Mock.Of<IBuildEngine>();
            using var stream = new MemoryStream();

            // Act
            taskInstance.WriteBootJson(stream, "MyEntrypointAssembly");

            // Assert
            var parsedContent = ParseBootData(stream);
            parsedContent.cacheBootResources.Should().Be(flagValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanSpecifyDebugBuild(bool flagValue)
        {
            // Arrange
            var taskInstance = new GenerateBlazorWebAssemblyBootJson { DebugBuild = flagValue };
            taskInstance.BuildEngine = Mock.Of<IBuildEngine>();
            
            using var stream = new MemoryStream();

            // Act
            taskInstance.WriteBootJson(stream, "MyEntrypointAssembly");

            // Assert
            var parsedContent = ParseBootData(stream);
            parsedContent.debugBuild.Should().Be(flagValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CanSpecifyLinkerEnabled(bool flagValue)
        {
            // Arrange
            var taskInstance = new GenerateBlazorWebAssemblyBootJson { LinkerEnabled = flagValue };
            taskInstance.BuildEngine = Mock.Of<IBuildEngine>();

            using var stream = new MemoryStream();

            // Act
            taskInstance.WriteBootJson(stream, "MyEntrypointAssembly");

            // Assert
            var parsedContent = ParseBootData(stream);
            parsedContent.linkerEnabled.Should().Be(flagValue);
        }

        private static BootJsonData ParseBootData(Stream stream)
        {
            stream.Position = 0;
            var serializer = new DataContractJsonSerializer(
                typeof(BootJsonData),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            return (BootJsonData)serializer.ReadObject(stream);
        }

        private static ITaskItem CreateResourceTaskItem(params (string key, string value)[] values)
        {
            var mock = new Mock<ITaskItem>();

            foreach (var (key, value) in values)
            {
                mock.Setup(m => m.GetMetadata(key)).Returns(value);
            }
            return mock.Object;
        }
    }
}
