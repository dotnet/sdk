// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using static Microsoft.NET.Publish.Tests.PublishTestUtils;
using static Microsoft.NET.Publish.Tests.ILLinkTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    // this test class is split up arbitrarily so Helix can run tests in multiple workitems
    public class GivenThatWeWantToRunILLink2b : SdkTest
    {
        public GivenThatWeWantToRunILLink2b(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ILLink_ignores_host_config_settings_with_link_false()
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName,
                // Reference the classlib to ensure its XML is processed.
                addAssemblyReference: true,
                // Set up a conditional feature substitution for the "FeatureDisabled" property
                modifyReferencedProject: (referencedProject) => AddFeatureDefinition(referencedProject, referenceProjectName));
            var testAsset = TestAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                // Set a matching RuntimeHostConfigurationOption, with Trim = "false"
                .WithProjectChanges(project => AddRuntimeConfigOption(project, trim: false))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                    $"/p:_ExtraTrimmerArgs=--action link {referenceProjectName}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var referenceDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(referenceDll).Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "FeatureAPI").Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "get_FeatureDisabled").Should().BeTrue();
            // Check that the feature substitution did not apply
            DoesImageHaveMethod(referenceDll, "FeatureImplementation").Should().BeTrue();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_runs_incrementally(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            WaitForUtcNowToAdvance();

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            semaphoreFirstModifiedTime.Should().Be(semaphoreSecondModifiedTime);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("netcoreapp3.1")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void ILLink_old_defaults_keep_nonframework(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/v:n", $"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{UnusedFrameworkAssembly}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, UnusedFrameworkAssembly).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ILLink_net7_defaults_trim_nonframework()
        {
            string targetFramework = "net7.0";
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/v:n", $"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{UnusedFrameworkAssembly}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeFalse();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeFalse();
            DoesDepsFileHaveAssembly(depsFile, UnusedFrameworkAssembly).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_does_not_include_leftover_artifacts_on_second_run(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName, referenceProjectIdentifier: targetFramework);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(linkedDirectory, "Link.semaphore");

            // Link, keeping classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            var publishedDllKeptFirstTimeOnly = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var linkedDllKeptFirstTimeOnly = Path.Combine(linkedDirectory, $"{referenceProjectName}.dll");
            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeTrue();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeTrue();

            // Delete kept dll from publish output (works around lack of incremental publish)
            File.Delete(publishedDllKeptFirstTimeOnly);

            // Remove root descriptor to change the linker behavior.
            WaitForUtcNowToAdvance();
            // File.SetLastWriteTimeUtc(Path.Combine(testAsset.TestRoot, testProject.Name, $"{projectName}.cs"), DateTime.UtcNow);
            testAsset = testAsset.WithProjectChanges(project => RemoveRootDescriptor(project));

            // Link, discarding classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            // Check that the linker actually ran again
            semaphoreFirstModifiedTime.Should().NotBe(semaphoreSecondModifiedTime);

            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeFalse();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeFalse();

            // "linked" intermediate directory does not pollute the publish output
            Directory.Exists(Path.Combine(publishDirectory, "linked")).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_keeps_symbols_by_default(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(linkedPdb).Should().BeTrue();

            var intermediatePdbSize = new FileInfo(intermediatePdb).Length;
            var linkedPdbSize = new FileInfo(linkedPdb).Length;
            var publishPdbSize = new FileInfo(publishedPdb).Length;

            if (!Net10Plus.Any(tfm => (string)tfm[0] == targetFramework))
            {
                // Check disabled for net10.0+ due to https://github.com/dotnet/sdk/issues/48633
                linkedPdbSize.Should().BeLessThanOrEqualTo(intermediatePdbSize);
            }
            publishPdbSize.Should().Be(linkedPdbSize);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_removes_symbols_when_debugger_support_is_disabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(TestAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:DebuggerSupport=false").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(intermediatePdb).Should().BeTrue();
            File.Exists(linkedPdb).Should().BeFalse();
            File.Exists(publishedPdb).Should().BeFalse();
        }
    }
}
