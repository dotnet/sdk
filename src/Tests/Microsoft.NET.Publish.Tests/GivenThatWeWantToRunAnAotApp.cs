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
        [InlineData(LatestTfm)]        
        public void NativeAot_only_runs_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            targetFramework = "net7.0";
            var rid = "win-x64";

            var testProject = CreateTestProjectForNativeAotTesting(targetFramework, projectName);
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = rid;
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                //.Execute()
                .Execute(@"-bl:D:\Work\Core\Test\NativeAOT\4_3\bin\GivenThatWeWantToRunAnAotApp.binlog")
//                .Execute(@"-pp:C:\Work\Core\Test\NativeAOT\4_3\bin\GivenThatWeWantToRunAnAotApp.txt")
                .Should().Pass();


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
