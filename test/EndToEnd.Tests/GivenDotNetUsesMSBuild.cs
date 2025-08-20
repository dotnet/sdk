// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EndToEnd.Tests
{
    public class GivenDotNetUsesMSBuild(ITestOutputHelper log) : SdkTest(log)
    {
        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            string projectDirectory = _testAssetsManager.CreateTestDirectory().Path;

            string[] newArgs = ["console", "--no-restore"];
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            new BuildCommand(Log, projectDirectory)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectDirectory)
                .Execute("/bl:helixfailure.binlog")
                .Should().Pass()
                    .And.HaveStdOutContaining("Hello, World!");

            var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
            binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            new CleanCommand(Log, projectDirectory)
                .Execute()
                .Should().Pass();

            binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ItCanRunToolsInACSProj()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                        new XAttribute("Include", "dotnet-portable"),
                        new XAttribute("Version", "1.0.0")));

                    project.Root.Add(itemGroup);
                });

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new RestoreCommand(testInstance)
                .Execute()
                .Should().Pass();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("portable")
                .Should().Pass()
                    .And.HaveStdOutContaining("Hello Portable World!");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), 
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ItCanRunToolsThatPrefersTheCliRuntimeEvenWhenTheToolItselfDeclaresADifferentRuntime()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                                    new XAttribute("Include", "dotnet-prefercliruntime"),
                                    new XAttribute("Version", "1.0.0")));

                    project.Root.Add(itemGroup);
                });

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new RestoreCommand(testInstance)
                .Execute()
                .Should().Pass();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("prefercliruntime")
                .Should().Pass()
                    .And.HaveStdOutContaining("Hello I prefer the cli runtime World!");
        }
    }
}
