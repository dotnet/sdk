// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithPublishTargets
    {
        private TestAssetsManager _testAssetsManager;

        public GivenThatWeWantToPublishAProjectWithPublishTargets()
        {
            _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;
        }

        [Fact]
        public void It_executes_targets_that_extend_publish_target()
        {
            var publishTargetsAsset = _testAssetsManager
                .CopyTestAsset("PublishTargets")
                .WithSource()
                .Restore();

            File.Copy(
                Path.Combine(publishTargetsAsset.SourceRoot, "obj", "PublishTargets.csproj.tool.g.targets"),
                Path.Combine(publishTargetsAsset.TestRoot, "obj", "PublishTargets.csproj.tool.g.targets"));

            var publishCommand = new PublishCommand(Stage0MSBuild, publishTargetsAsset.TestRoot);
            var timestamp = DateTime.UtcNow.ToString();
            var publishResult = publishCommand.Execute("/p:PublishTimestamp=" + timestamp);

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("netstandard1.0");

            var publishTargetFiles = new []
            {
                "afterpublish.txt",
                "beforepublish.txt",
                "publishdependson.txt",
                "importedpublishdependson.txt"
            };

            publishDirectory.Should().OnlyHaveFiles(new[]
            {
                "PublishTargets.dll",
                "PublishTargets.pdb",
                "PublishTargets.deps.json",
            }.Concat(publishTargetFiles));

            foreach (var file in publishTargetFiles)
            {
                var path = Path.Combine(publishDirectory.FullName, file);
                File.ReadAllText(path)?.Trim()
                    .Should().Be(timestamp);
            }
        }
    }
}