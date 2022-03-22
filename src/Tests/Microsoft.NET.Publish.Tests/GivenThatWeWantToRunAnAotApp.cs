// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToRunAnAotApp : SdkTest
    {
        public GivenThatWeWantToRunAnAotApp(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        //[InlineData("net6.0", true)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void NativeAot_only_runs_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            targetFramework = "net6.0";
            var rid = "win-x64";//EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForNativeAotTesting(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishNativeAot=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

        }

        private TestProject CreateTestProjectForNativeAotTesting(
            string targetFramework,
            string mainProjectName)
        {
            var testProject = new TestProject()
            {
                Name = mainProjectName,
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.SourceFiles[$"{mainProjectName}.cs"] = @"
using System;
class Test
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Hello, World!"");
    }
}";
            return testProject;
        }


    }
}
