// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAnAotApp : SdkTest
    {
        private readonly string RuntimeIdentifier = $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}";

        public GivenThatWeWantToPublishAnAotApp(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(LatestTfm)]
        public void ILLink_aot_analyzer_warnings_are_produced(string targetFramework)
        {
            var projectName = "ILLinkAotAnalyzerWarningsApp";
            var testProject = CreateTestProjectWithAotAnalyzerWarnings(targetFramework, projectName, true);
            testProject.AdditionalProperties["EnableAotAnalyzer"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute(RuntimeIdentifier)
                .Should().Pass()
                .And.HaveStdOutContaining("(8,9): warning IL3050")
                .And.HaveStdOutContaining("(18,12): warning IL3052");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(LatestTfm)]
        public void ILLink_linker_warnings_not_produced_if_not_set(string targetFramework)
        {
            var projectName = "ILLinkAotAnalyzerWarningsApp";
            var testProject = CreateTestProjectWithAotAnalyzerWarnings(targetFramework, projectName, true);
            // Inactive linker settings should have no effect on the aot analyzer,
            // unless PublishTrimmed is also set.
            testProject.AdditionalProperties["EnableAotAnalyzer"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute(RuntimeIdentifier)
                .Should().Pass()
                .And.NotHaveStdOutContaining("IL2026");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(LatestTfm)]
        public void NativeAot_only_runs_when_switch_is_enabled(string targetFramework)
        {

            // Need to enable this in a Linux distro by adding the pre-reqs to a suitable container
            // https://github.com/dotnet/sdk/issues/24983
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var projectName = "AotPublishWithWarnings";
                var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

                // PublishAot should enable the EnableAotAnalyzer
                var testProject = CreateTestProjectWithAotAnalyzerWarnings(targetFramework, projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "true";
                testProject.AdditionalProperties["RuntimeIdentifier"] = rid;
                var testAsset = _testAssetsManager.CreateTestProject(testProject);

                var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
                publishCommand
                    .Execute()
                    .Should().Pass()
                    .And.HaveStdOutContaining("warning IL3050");

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

                var publishedExe = Path.Combine(publishDirectory, $"{projectName}.exe");

                // The exe exist and should be native
                File.Exists(publishedExe).Should().BeTrue();
                IsNativeImage(publishedExe).Should().BeTrue();
            }

        }

        private TestProject CreateTestProjectWithAotAnalyzerWarnings(string targetFramework, string projectName, bool isExecutable)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = isExecutable
            };

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
class C
{
    static void Main()
    {
        ProduceAotAnalysisWarning();
        ProduceLinkerAnalysisWarning();
    }

    [RequiresDynamicCode(""Aot analysis warning"")]
    static void ProduceAotAnalysisWarning()
    {
    }

    [RequiresDynamicCode(""Aot analysis warning"")]
    static C()
    {
    }

    [RequiresUnreferencedCode(""Linker analysis warning"")]
    static void ProduceLinkerAnalysisWarning()
    {
    }
}";

            return testProject;
        }

        private static bool IsNativeImage(string path)
        {
            bool returnValue=false;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var peReader = new PEReader(fs))
            {
                try
                {
                    var metadataReader = peReader.GetMetadataReader();
                }catch(InvalidOperationException)
                {
                    returnValue = true;
                }
                catch(Exception){}
            }
            return returnValue;
        }


    }
}
