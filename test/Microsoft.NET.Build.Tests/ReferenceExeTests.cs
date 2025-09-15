// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests
{
    public class ReferenceExeTests : SdkTest
    {
        public ReferenceExeTests(ITestOutputHelper log) : base(log)
        {
        }

        private string MainProjectTargetFrameworks = "";

        private string ReferenceProjectTargetFrameworks = "";

        private bool MainRuntimeIdentifier { get; set; }

        private bool MainSelfContained { get; set; }

        private bool ReferencedSelfContained { get; set; }

        private bool TestWithPublish { get; set; } = false;

        private bool PublishTrimmed = false;

        private bool ReferenceExeInCode = false;

        private TestProject MainProject { get; set; }

        private TestProject ReferencedProject { get; set; }

        private void CreateProjects()
        {
            MainProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = MainProjectTargetFrameworks != "" ? MainProjectTargetFrameworks : ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            MainProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.8.26"));
            var mainProjectSrc = @"
using System;
using Humanizer;
Console.WriteLine(""MainProject"".Humanize());";

            if (PublishTrimmed)
            {
                MainProject.AdditionalProperties["PublishTrimmed"] = "true";

                // If we're fully trimming, unless the trimmed project contains an explicit reference in code
                // to the referenced project, it will get trimmed away
                if (ReferenceExeInCode)
                {
                    mainProjectSrc += @"
// Always false, but the trimmer doesn't know that
if (string.Empty.Length > 0)
{
    ReferencedExeProgram.Main();
}";

                }
            }


            MainProject.SourceFiles["Program.cs"] = mainProjectSrc;

            if (MainRuntimeIdentifier)
            {
                MainProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
            }

            if (MainSelfContained)
            {
                MainProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
                MainProject.SelfContained = "true";
            }

            ReferencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ReferenceProjectTargetFrameworks != "" ? ReferenceProjectTargetFrameworks : ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true,
                IsExe = true,
            };

            if (ReferencedSelfContained)
            {
                ReferencedProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid();
                ReferencedProject.SelfContained = "true";
            }

            //  Use a lower version of a library in the referenced project
            ReferencedProject.PackageReferences.Add(new TestPackageReference("Humanizer", "2.7.9"));
            ReferencedProject.SourceFiles["Program.cs"] = @"
using Humanizer;
public class ReferencedExeProgram
{
    public static void Main()
    {
        System.Console.WriteLine(""ReferencedProject"".Humanize());
    }
}";

            MainProject.ReferencedProjects.Add(ReferencedProject);
        }

        private void RunTest(string buildFailureCode = null, [CallerMemberName] string callingMethod = null)
        {
            var testProjectInstance = _testAssetsManager.CreateTestProject(MainProject, callingMethod: callingMethod, identifier: MainSelfContained.ToString() + "_" + ReferencedSelfContained.ToString());

            string outputDirectory;

            TestCommand buildOrPublishCommand;

            if (TestWithPublish)
            {
                var publishCommand = new PublishCommand(testProjectInstance);

                outputDirectory = publishCommand.GetOutputDirectory(MainProject.TargetFrameworks, runtimeIdentifier: MainProject.RuntimeIdentifier).FullName;

                buildOrPublishCommand = publishCommand;
            }
            else
            {
                var buildCommand = new BuildCommand(testProjectInstance);

                outputDirectory = buildCommand.GetOutputDirectory(MainProject.TargetFrameworks, runtimeIdentifier: MainProject.RuntimeIdentifier).FullName;

                buildOrPublishCommand = buildCommand;
            }

            if (buildFailureCode == null)
            {
                buildOrPublishCommand.Execute()
                    .Should()
                    .Pass();

                var mainExePath = Path.Combine(outputDirectory, MainProject.Name + Constants.ExeSuffix);

                var referencedExePath = Path.Combine(outputDirectory, ReferencedProject.Name + Constants.ExeSuffix);

                new RunExeCommand(Log, mainExePath)
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOut("Main project");


                var referencedExeResult = new RunExeCommand(Log, referencedExePath)
                    .Execute();

                // If we're trimming and didn't reference the exe in source we would expect it to be trimmed from the output
                if (PublishTrimmed && !ReferenceExeInCode)
                {
                    referencedExeResult
                        .Should()
                        .Fail()
                        .And
                        .HaveStdErrContaining("The application to execute does not exist");
                }
                else
                {
                    referencedExeResult
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOut("Referenced project");
                }
            }
            else
            {
                //  Build should not succeed
                buildOrPublishCommand.Execute()
                    .Should()
                    .Fail()
                    .And
                    .HaveStdOutContaining(buildFailureCode);
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void ReferencedExeCanRun(bool mainSelfContained, bool referencedSelfContained)
        {
            MainSelfContained = mainSelfContained;
            ReferencedSelfContained = referencedSelfContained;

            CreateProjects();

            RunTest();
        }

        [Fact]
        public void ReferencedExeWithLowerTargetFrameworkCanRun()
        {
            MainSelfContained = false;
            ReferencedSelfContained = false;

            CreateProjects();

            ReferencedProject.TargetFrameworks = ToolsetInfo.CurrentTargetFramework;
            ReferencedProject.AdditionalProperties["LangVersion"] = "9.0";

            RunTest();
        }

        //  Having a self-contained and a framework-dependent app in the same folder is not supported (due to the way the host works).
        //  The referenced app will fail to run.  See here for more details: https://github.com/dotnet/sdk/pull/14488#issuecomment-725406998
        [Theory]
        [InlineData(true, false, "NETSDK1150")]
        [InlineData(false, true, "NETSDK1151")]
        public void ReferencedExeFailsToBuildOnOlderTargetFrameworks(bool mainSelfContained, bool referencedSelfContained, string expectedFailureCode)
        {
            MainSelfContained = mainSelfContained;
            ReferencedSelfContained = referencedSelfContained;
            ReferenceProjectTargetFrameworks = "net7.0";
            // the main project tfm will be 8.0 or higher to make sure the error uses the tfm of the referenced project and not the main project.

            CreateProjects();

            RunTest(expectedFailureCode);
        }

        [Fact]
        public void ReferencedExeDoesNotFailToBuildWith8PlusTargetFrameworks()
        {
            MainSelfContained = false;
            MainRuntimeIdentifier = true;
            CreateProjects();

            RunTest();
        }

        [Fact]
        public void ReferencedExeCanRunWhenReferencesExeWithSelfContainedMismatchForDifferentTargetFramework()
        {
            MainSelfContained = true;
            ReferencedSelfContained = false;

            CreateProjects();

            //  Reference project which is self-contained for net5.0, not self-contained for net5.0-windows.
            ReferencedProject.TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.CurrentTargetFramework}-windows";
            ReferencedProject.ProjectChanges.Add(project =>
            {
                var ns = project.Root.Name.Namespace;

                var propertyGroup = new XElement(ns + "PropertyGroup",
                    new XAttribute("Condition", $"'$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"));

                propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", EnvironmentInfo.GetCompatibleRid()));
                propertyGroup.Add(new XElement(ns + "SelfContained", "true"));

                project.Root.Add(propertyGroup);
            });


            RunTest();
        }

        [Fact]
        public void ReferencedExeFailsToBuildWhenReferencesExeWithSelfContainedMismatchForSameTargetFramework()
        {
            MainSelfContained = true;
            ReferencedSelfContained = false;

            CreateProjects();

            //  Reference project which is self-contained for net5.0-windows, not self-contained for net5.0.
            ReferencedProject.TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};{ToolsetInfo.CurrentTargetFramework}-windows";
            ReferencedProject.ProjectChanges.Add(project =>
            {
                var ns = project.Root.Name.Namespace;

                project.Root.Element(ns + "PropertyGroup")
                    .Add(XElement.Parse($@"<RuntimeIdentifier Condition=""'$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}-windows'"">" + EnvironmentInfo.GetCompatibleRid() + "</RuntimeIdentifier>"));
            });

            RunTest("NETSDK1150");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReferencedExeCanRunWhenPublished(bool selfContained)
        {
            MainSelfContained = selfContained;
            ReferencedSelfContained = selfContained;

            TestWithPublish = true;

            CreateProjects();

            RunTest();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReferencedExeCanRunWhenPublishedWithTrimming(bool referenceExeInCode)
        {
            MainSelfContained = true;
            ReferencedSelfContained = true;

            TestWithPublish = true;
            PublishTrimmed = true;
            ReferenceExeInCode = referenceExeInCode;

            CreateProjects();

            RunTest(callingMethod: System.Reflection.MethodBase.GetCurrentMethod().ToString()
                .Replace("Void ", "")
                .Replace("Boolean", referenceExeInCode.ToString()));
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [CombinatorialData]
        public void TestProjectCanReferenceExe(
            // Note: xunit.v3 is always a "real" executable even with VSTest. So it's irrelevant here.
            [CombinatorialValues("nunit", "mstest")] string testTemplateName,
            bool setSelfContainedProperty)
        {
            var testConsoleProject = new TestProject("ConsoleApp")
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            if (setSelfContainedProperty)
            {
                testConsoleProject.SelfContained = "true";
            }

            var testAsset = _testAssetsManager.CreateTestProject(testConsoleProject, identifier: testTemplateName);

            var testProjectDirectory = Path.Combine(testAsset.TestRoot, "TestProject");
            Directory.CreateDirectory(testProjectDirectory);

            new DotnetNewCommand(Log, testTemplateName)
                .WithVirtualHive()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log, "add", "reference", ".." + Path.DirectorySeparatorChar + testConsoleProject.Name)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

        }

        [Theory]
        [CombinatorialData]
        public void SelfContainedExecutableCannotBeReferencedByNonSelfContainedMTPTestProject(bool setIsTestingPlatformApplicationEarly)
        {
            // The setup of this test is as follows:
            // ConsoleApp is a self-contained executable project.
            // MTPTestProject is an executable test project that references ConsoleApp.
            // Building MTPTestProject should fail because it references a self-contained executable project.
            // A self-contained executable cannot be referenced by a non self-contained executable.
            var testConsoleProjectSelfContained = new TestProject("ConsoleApp")
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                SelfContained = "true",
            };

            var mtpNotSelfContained = new TestProject("MTPTestProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            mtpNotSelfContained.AdditionalProperties["IsTestProject"] = "true";

            if (setIsTestingPlatformApplicationEarly)
            {
                mtpNotSelfContained.AdditionalProperties["IsTestingPlatformApplication"] = "true";
            }

            mtpNotSelfContained.ReferencedProjects.Add(testConsoleProjectSelfContained);

            var testAssetMTP = _testAssetsManager.CreateTestProject(mtpNotSelfContained);

            var mtpProjectDirectory = Path.Combine(testAssetMTP.Path, "MTPTestProject");
            
            if (!setIsTestingPlatformApplicationEarly)
            {
                File.WriteAllText(Path.Combine(mtpProjectDirectory, "Directory.Build.targets"), """
                    <Project>
                        <PropertyGroup>
                            <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
                        </PropertyGroup>
                    </Project>
                    """);
            }

            var result = new BuildCommand(Log, mtpProjectDirectory).Execute();
            result.Should().Fail().And.HaveStdOutContaining("NETSDK1151");
        }

        [Theory]
        [CombinatorialData]
        public void MTPNonSelfContainedExecutableCannotBeReferencedBySelfContained(bool setIsTestingPlatformApplicationEarly)
        {
            // The setup of this test is as follows:
            // ConsoleApp is a self-contained executable project, which references a non-self-contained MTP executable test project.
            // Building ConsoleApp should fail because it references a non-self-contained MTP executable project.
            // A non self-contained executable cannot be referenced by a self-contained executable.
            var testConsoleProjectSelfContained = new TestProject("ConsoleApp")
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                SelfContained = "true",
            };

            var mtpNotSelfContained = new TestProject("MTPTestProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            mtpNotSelfContained.AdditionalProperties["IsTestProject"] = "true";

            if (setIsTestingPlatformApplicationEarly)
            {
                mtpNotSelfContained.AdditionalProperties["IsTestingPlatformApplication"] = "true";
            }

            testConsoleProjectSelfContained.ReferencedProjects.Add(mtpNotSelfContained);

            var testAssetSelfContained = _testAssetsManager.CreateTestProject(testConsoleProjectSelfContained);
            
            if (!setIsTestingPlatformApplicationEarly)
            {
                File.WriteAllText(Path.Combine(testAssetSelfContained.TestRoot, mtpNotSelfContained.Name, "Directory.Build.targets"), """
                    <Project>
                        <PropertyGroup>
                            <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
                        </PropertyGroup>
                    </Project>
                    """);
            }

            var consoleAppDirectory = Path.Combine(testAssetSelfContained.Path, testConsoleProjectSelfContained.Name);

            var result = new BuildCommand(Log, consoleAppDirectory).Execute();
            result.Should().HaveStdOutContaining("NETSDK1150").And.ExitWith(1);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [CombinatorialData]
        public void ExeProjectCanReferenceTestProject(
            // Note: xunit.v3 is always a "real" executable even with VSTest. So it's irrelevant here.
            [CombinatorialValues("nunit", "mstest")] string testTemplateName,
            bool setSelfContainedProperty,
            bool buildWithSelfContainedFromCommandLine)
        {
            var testConsoleProject = new TestProject("ConsoleApp")
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            if (setSelfContainedProperty)
            {
                testConsoleProject.SelfContained = "true";
            }

            var testAsset = _testAssetsManager.CreateTestProject(testConsoleProject, identifier: testTemplateName);

            var testProjectDirectory = Path.Combine(testAsset.TestRoot, "TestProject");
            Directory.CreateDirectory(testProjectDirectory);

            new DotnetNewCommand(Log, testTemplateName)
                .WithVirtualHive()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            string consoleProjectDirectory = Path.Combine(testAsset.Path, testConsoleProject.Name);

            new DotnetCommand(Log, "add", "reference", ".." + Path.DirectorySeparatorChar + "TestProject")
                .WithWorkingDirectory(consoleProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            new BuildCommand(testAsset)
                .WithWorkingDirectory(consoleProjectDirectory)
                .Execute(buildWithSelfContainedFromCommandLine ? ["-p:SelfContained=true"] : Array.Empty<string>())
                .Should()
                .Pass();
        }

        [Theory]
        [CombinatorialData]
        public void MTPCanBeBuiltAsSelfContained(bool setIsTestingPlatformApplicationEarly)
        {
            var mtpSelfContained = new TestProject("MTPTestProject")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SelfContained = "true",
            };

            mtpSelfContained.AdditionalProperties["IsTestProject"] = "true";

            if (setIsTestingPlatformApplicationEarly)
            {
                mtpSelfContained.AdditionalProperties["IsTestingPlatformApplication"] = "true";
            }

            var testAssetMTP = _testAssetsManager.CreateTestProject(mtpSelfContained);

            var mtpProjectDirectory = Path.Combine(testAssetMTP.Path, mtpSelfContained.Name);

            if (!setIsTestingPlatformApplicationEarly)
            {
                File.WriteAllText(Path.Combine(mtpProjectDirectory, "Directory.Build.targets"), """
                    <Project>
                        <PropertyGroup>
                            <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
                        </PropertyGroup>
                    </Project>
                    """);
            }

            new BuildCommand(Log, mtpProjectDirectory)
                .Execute()
                .Should()
                .Pass();
        }
    }
}
