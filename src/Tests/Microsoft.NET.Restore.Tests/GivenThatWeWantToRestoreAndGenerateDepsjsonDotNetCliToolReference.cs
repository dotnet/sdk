// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToRestoreAndGenerateDepsjsonDotNetCliToolReference : SdkTest
    {
        public GivenThatWeWantToRestoreAndGenerateDepsjsonDotNetCliToolReference(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_can_generate_correct_deps_json()
        {
            TestProject toolProject = new TestProject()
            {
                Name = "DotNetCliToolReferenceProject",
                IsSdkProject = true,
                IsExe = true,
                TargetFrameworks = "netcoreapp1.0",
            };

            toolProject.AdditionalProperties.Add("PackageTargetFallback", "$(PackageTargetFallback);portable-net45+win8+wp8+wpa81;");
            toolProject.AdditionalProperties.Add("RestorePackagesPath", "packages");
            toolProject.PackageReferences.Add(
                new TestPackageReference(
                    id: "Microsoft.VisualStudio.Web.CodeGeneration.Design",
                    version: "1.0.3",
                    nupkgPath: null));
            toolProject.DotNetCliToolReferences.Add(
                new TestPackageReference(id: "Microsoft.VisualStudio.Web.CodeGeneration.Tools",
                                         version: "1.0.3",
                                         nupkgPath: null));

            TestAsset toolProjectInstance = _testAssetsManager.CreateTestProject(toolProject, identifier: toolProject.Name);

            NuGetConfigWriter.Write(toolProjectInstance.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed);

            RestoreWithRetryDueToDownloadFromWeb(toolProject, toolProjectInstance);

            var runProjectToolCommand = new DotnetCommand(Log, "aspnet-codegenerator")
            {
                WorkingDirectory = Path.Combine(toolProjectInstance.TestRoot, toolProject.Name)
            };

            runProjectToolCommand.Execute().Should().Pass();
        }

        private void RestoreWithRetryDueToDownloadFromWeb(TestProject toolProject, TestAsset toolProjectInstance)
        {
            var restoreResult = toolProjectInstance.GetRestoreCommand(log: Log, relativePath: toolProject.Name)
                .Execute("/v:n");

            if (restoreResult.ExitCode != 0)
            {
                toolProjectInstance.Restore(Log, toolProject.Name, "/v:n");
            }
        }
    }
}
