// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.Build.Tests
{
    public class RuntimeIdentifierGraphTests : SdkTest
    {
        public RuntimeIdentifierGraphTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("net7.0", null, true)]
        [InlineData("net8.0", null, false)]
        [InlineData("net7.0", "false", false)]
        [InlineData("net8.0", "true", true)]
        public void ItUsesCorrectRuntimeIdentifierGraph(string targetFramework, string useRidGraphValue, bool shouldUseFullRidGraph)
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            if (useRidGraphValue != null)
            {
                testProject.AdditionalProperties["UseRidGraph"] = useRidGraphValue;
            }

            testProject.RecordProperties("RuntimeIdentifierGraphPath");

            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework + "_" + (useRidGraphValue ?? "null"));

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var runtimeIdentifierGraphPath = testProject.GetPropertyValues(testAsset.TestRoot)["RuntimeIdentifierGraphPath"];

            if (shouldUseFullRidGraph)
            {
                Path.GetFileName(runtimeIdentifierGraphPath).Should().Be("RuntimeIdentifierGraph.json");
            }
            else
            {
                Path.GetFileName(runtimeIdentifierGraphPath).Should().Be("PortableRuntimeIdentifierGraph.json");
            }
        }
    }
}
