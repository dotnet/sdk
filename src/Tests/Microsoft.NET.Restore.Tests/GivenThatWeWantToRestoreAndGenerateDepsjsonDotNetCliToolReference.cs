// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToRestoreAndGenerateDepsjsonDotNetCliToolReference : SdkTest
    {
        private const string ProjectToolVersion = "1.0.3";
        private const string CodeGenerationPackageName = "microsoft.visualstudio.web.codegeneration.tools";
        private const string ExpectedProjectToolRestoreTargetFrameworkMoniker = "netcoreapp2.2";
        private const string FolderNameForRestorePackages = "packages";

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
            toolProject.AdditionalProperties.Add("RestorePackagesPath", FolderNameForRestorePackages);
            toolProject.PackageReferences.Add(
                new TestPackageReference(
                    id: "Microsoft.VisualStudio.Web.CodeGeneration.Design",
                    version: ProjectToolVersion,
                    nupkgPath: null));
            toolProject.DotNetCliToolReferences.Add(
                new TestPackageReference(id: CodeGenerationPackageName,
                                         version: ProjectToolVersion,
                                         nupkgPath: null));

            TestAsset toolProjectInstance = _testAssetsManager.CreateTestProject(toolProject, identifier: toolProject.Name);

            NuGetConfigWriter.Write(toolProjectInstance.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed);

            RestoreCommand restoreCommand = toolProjectInstance.GetRestoreCommand(log: Log, relativePath: toolProject.Name);
            var restoreResult = restoreCommand
                .Execute("/v:n");

            if (restoreResult.ExitCode != 0)
            {
                // retry once since it downloads from the web
                toolProjectInstance.Restore(Log, toolProject.Name, "/v:n");
            }

            AssertRestoreTargetFramework(restoreCommand);

            var runProjectToolCommand = new DotnetCommand(Log, "aspnet-codegenerator")
            {
                WorkingDirectory = Path.Combine(toolProjectInstance.TestRoot, toolProject.Name)
            };

            runProjectToolCommand.Execute().Should().Pass();
        }

        private static void AssertRestoreTargetFramework(RestoreCommand restoreCommand)
        {
            var assetsJsonPath = Path.Combine(restoreCommand.ProjectRootPath,
                                              FolderNameForRestorePackages,
                                              ".tools",
                                              CodeGenerationPackageName,
                                              ProjectToolVersion,
                                              ExpectedProjectToolRestoreTargetFrameworkMoniker,
                                              "project.assets.json");
            LockFile lockFile = LockFileUtilities.GetLockFile(assetsJsonPath, NullLogger.Instance);
            lockFile.Targets.Single().TargetFramework
                .Should().Be(NuGetFramework.Parse(ExpectedProjectToolRestoreTargetFrameworkMoniker),
                "Restore target framework should be caped at netcoreapp2.2 due to moving away from project tools." +
                "Even when SDK's TFM is higher and the project's TFM is netcoreapp1.0");
        }
    }
}
