// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.PortableExecutable;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;
using Newtonsoft.Json.Linq;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAnAotApp : SdkTest
    {
        private readonly string RuntimeIdentifier = $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}";

        private readonly string ExplicitPackageVersion = "7.0.0-rc.2.22456.11";

        public GivenThatWeWantToPublishAnAotApp(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.8.0")]
        [MemberData(nameof(Net7Plus), MemberType = typeof(PublishTestUtils))]
        public void NativeAot_hw_runs_with_no_warnings_when_PublishAot_is_enabled(string targetFramework)
        {
            if (targetFramework == "net7.0" && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // 7.0 is not supported on Mac
                return;
            }

            var projectName = "HellowWorldNativeAotApp";

            var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            // Linux symbol files are embedded and require additional steps to be stripped to a separate file
            // assumes /bin (or /usr/bin) are in the PATH
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                testProject.AdditionalProperties["StripSymbols"] = "true";
            }
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute($"/p:UseCurrentRuntimeIdentifier=true", "/p:SelfContained=true")
                .Should().Pass()
                .And.NotHaveStdOutContaining("IL2026")
                .And.NotHaveStdErrContaining("NETSDK1179")
                .And.NotHaveStdErrContaining("warning")
                .And.NotHaveStdOutContaining("warning");

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var sharedLibSuffix = GetSharedLibSuffix();
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}{sharedLibSuffix}");
            var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");

            // NativeAOT published dir should not contain a non-host stand alone package
            File.Exists(publishedDll).Should().BeFalse();
            // The exe exist and should be native
            File.Exists(publishedExe).Should().BeTrue();
            DoSymbolsExist(publishDirectory, testProject.Name).Should().BeTrue($"{publishDirectory} should contain {testProject.Name} symbol");
            IsNativeImage(publishedExe).Should().BeTrue();

            GetKnownILCompilerPackVersion(testAsset, targetFramework, out string expectedVersion);
            CheckIlcVersions(testAsset, targetFramework, rid, expectedVersion);

            var command = new RunExeCommand(Log, publishedExe)
                .Execute().Should().Pass()
                .And.HaveStdOutContaining("Hello World");
        }

        [RequiresMSBuildVersionTheory("17.8.0")]
        [MemberData(nameof(Net7Plus), MemberType = typeof(PublishTestUtils))]
        public void NativeAot_hw_runs_with_no_warnings_when_PublishAot_is_false(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var projectName = "HellowWorldNativeAotApp";
                var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

                var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "false";
                var testAsset = _testAssetsManager.CreateTestProject(testProject);

                var publishCommand = new PublishCommand(testAsset);
                publishCommand
                    .Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                    .Should().Pass()
                    .And.NotHaveStdOutContaining("IL2026")
                    .And.NotHaveStdErrContaining("NETSDK1179")
                    .And.NotHaveStdErrContaining("warning")
                    .And.NotHaveStdOutContaining("warning");

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
                var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
                var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");

                // PublishAot=false will be a normal publish
                File.Exists(publishedDll).Should().BeTrue();

                var command = new RunExeCommand(Log, publishedExe)
                    .Execute().Should().Pass()
                    .And.HaveStdOutContaining("Hello World");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_app_runs_in_debug_with_no_config_when_PublishAot_is_enabled(string targetFramework)
        {
            // NativeAOT application publish directory should not contain any <App>.deps.json or <App>.runtimeconfig.json
            // The test writes a key-value pair to the runtimeconfig file and checks that the app can access it
            var projectName = "NativeAotAppForConfigTestDbg";
            var projectConfiguration = "Debug";

            var testProject = CreateAppForConfigCheck(targetFramework, projectName, true);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["Configuration"] = projectConfiguration;
            // Linux symbol files are embedded and require additional steps to be stripped to a separate file
            // assumes /bin (or /usr/bin) are in the PATH
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                testProject.AdditionalProperties["StripSymbols"] = "true";
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                // populate a runtime config file with a key value pair
                // <RuntimeHostConfigurationOption Include="key1" Value="value1" />
                .WithProjectChanges(project => AddRuntimeConfigOption(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute($"/p:UseCurrentRuntimeIdentifier=true", "/p:SelfContained=true")
                .Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework, projectConfiguration);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, configuration: projectConfiguration, runtimeIdentifier: rid).FullName;
            var sharedLibSuffix = GetSharedLibSuffix();
            var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");
            var publishedRuntimeConfig = Path.Combine(publishDirectory, $"{testProject.Name}.runtimeconfig.json");
            var publishedDeps = Path.Combine(publishDirectory, $"{testProject.Name}.deps.json");

            // NativeAOT published dir should not contain a runtime configuration file
            File.Exists(publishedRuntimeConfig).Should().BeFalse();
            // NativeAOT published dir should not contain a dependency file
            File.Exists(publishedDeps).Should().BeFalse();
            // The exe exist and should be native
            File.Exists(publishedExe).Should().BeTrue();
            // There should be a debug file
            DoSymbolsExist(publishDirectory, testProject.Name).Should().BeTrue($"{publishDirectory} should contain {testProject.Name} symbol");
            IsNativeImage(publishedExe).Should().BeTrue();

            // The app accesses the runtime config file key-value pair
            var command = new RunExeCommand(Log, publishedExe)
                .Execute().Should().Pass();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_app_runs_in_release_with_no_config_when_PublishAot_is_enabled(string targetFramework)
        {
            var projectName = "NativeAotAppForConfigTestRel";
            var projectConfiguration = "Release";

            var testProject = CreateAppForConfigCheck(targetFramework, projectName, true);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["Configuration"] = projectConfiguration;
            // Linux symbol files are embedded and require additional steps to be stripped to a separate file
            // assumes /bin (or /usr/bin) are in the PATH
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                testProject.AdditionalProperties["StripSymbols"] = "true";
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                // populate a runtime config file with a key value pair
                // <RuntimeHostConfigurationOption Include="key1" Value="value1" />
                .WithProjectChanges(project => AddRuntimeConfigOption(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute($"/p:UseCurrentRuntimeIdentifier=true", "/p:SelfContained=true")
                .Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework, projectConfiguration);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, configuration: projectConfiguration, runtimeIdentifier: rid).FullName;
            var sharedLibSuffix = GetSharedLibSuffix();
            var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");
            var publishedRuntimeConfig = Path.Combine(publishDirectory, $"{testProject.Name}.runtimeconfig.json");
            var publishedDeps = Path.Combine(publishDirectory, $"{testProject.Name}.deps.json");

            // NativeAOT published dir should not contain a runtime configuration file
            File.Exists(publishedRuntimeConfig).Should().BeFalse();
            // NativeAOT published dir should not contain a dependency file
            File.Exists(publishedDeps).Should().BeFalse();
            // The exe exist and should be native
            File.Exists(publishedExe).Should().BeTrue();
            // There should be a debug file
            DoSymbolsExist(publishDirectory, testProject.Name).Should().BeTrue($"{publishDirectory} should contain {testProject.Name} symbol");
            IsNativeImage(publishedExe).Should().BeTrue();

            // The app accesses the runtime config file key-value pair
            var command = new RunExeCommand(Log, publishedExe)
                .Execute().Should().Pass();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_app_builds_with_config_when_PublishAot_is_enabled(string targetFramework)
        {
            // NativeAOT application publish directory should not contain any <App>.deps.json or <App>.runtimeconfig.json
            // But build step should preserve these files
            var projectName = "NativeAotAppForConfigTest";

            var testProject = CreateAppForConfigCheck(targetFramework, projectName, true);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                // populate a runtime config file with a key value pair
                // <RuntimeHostConfigurationOption Include="key1" Value="value1" />
                .WithProjectChanges(project => AddRuntimeConfigOption(project));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute($"/p:UseCurrentRuntimeIdentifier=true", "/p:SelfContained=true")
                .Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}{Constants.ExeSuffix}");
            var runtimeConfigPath = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");
            var depsPath = Path.Combine(outputDirectory, $"{projectName}.deps.json");

            File.Exists(assemblyPath).Should().BeTrue();
            // NativeAOT build dir should contain a runtime configuration file
            File.Exists(runtimeConfigPath).Should().BeTrue();
            // NativeAOT build dir should contain a dependency file
            File.Exists(depsPath).Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_hw_runs_with_PackageReference_PublishAot_is_enabled(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Test uses net7 package of Native AOT, which didn't exist in .NET 7
                return;
            }

            var projectName = "HellowWorldNativeAotApp";

            var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";

            // This will add a reference to a package that will also be automatically imported by the SDK
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.DotNet.ILCompiler", ExplicitPackageVersion));

            // Linux symbol files are embedded and require additional steps to be stripped to a separate file
            // assumes /bin (or /usr/bin) are in the PATH
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                testProject.AdditionalProperties["StripSymbols"] = "true";
            }
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute($"/p:UseCurrentRuntimeIdentifier=true", "/p:SelfContained=true")
                .Should().Pass()
            // Having an explicit package reference will generate a warning
            .And.HaveStdOutContaining("warning")
            .And.HaveStdOutContaining("Microsoft.DotNet.ILCompiler");

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var sharedLibSuffix = GetSharedLibSuffix();
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}{sharedLibSuffix}");
            var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");

            // NativeAOT published dir should not contain a non-host stand alone package
            File.Exists(publishedDll).Should().BeFalse();
            // The exe exist and should be native
            File.Exists(publishedExe).Should().BeTrue();
            DoSymbolsExist(publishDirectory, testProject.Name).Should().BeTrue($"{publishDirectory} should contain {testProject.Name} symbol");
            IsNativeImage(publishedExe).Should().BeTrue();

            var command = new RunExeCommand(Log, publishedExe)
                .Execute().Should().Pass()
                .And.HaveStdOutContaining("Hello World");

            CheckIlcVersions(testAsset, targetFramework, rid, ExplicitPackageVersion);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_hw_runs_with_PackageReference_PublishAot_is_empty(string targetFramework)
        {
            var projectName = "HellowWorldNativeAotApp";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);

            // This will add a reference to a package that will also be automatically imported by the SDK
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.DotNet.ILCompiler", ExplicitPackageVersion));

            // Linux symbol files are embedded and require additional steps to be stripped to a separate file
            // assumes /bin (or /usr/bin) are in the PATH
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                testProject.AdditionalProperties["StripSymbols"] = "true";
            }
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                .Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");

            // Not setting PublishAot to true will be a normal publish
            File.Exists(publishedDll).Should().BeTrue();

            var command = new RunExeCommand(Log, publishedExe)
                .Execute().Should().Pass()
                .And.HaveStdOutContaining("Hello World");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net7Plus), MemberType = typeof(PublishTestUtils))]
        public void NativeAot_hw_runs_with_cross_target_PublishAot_is_enabled(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (RuntimeInformation.OSArchitecture == Architecture.X64))
            {
                var projectName = "HellowWorldNativeAotApp";
                var rid = "win-arm64";

                var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "true";

                var testAsset = _testAssetsManager.CreateTestProject(testProject);

                var publishCommand = new PublishCommand(testAsset);
                publishCommand
                    .Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                    .Should().Pass();
                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
                var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
                var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");
                File.Exists(publishedDll).Should().BeFalse();
                File.Exists(publishedExe).Should().BeTrue();

                GetKnownILCompilerPackVersion(testAsset, targetFramework, out string expectedVersion);
                CheckIlcVersions(testAsset, targetFramework, rid, expectedVersion);
            }
        }


        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net7Plus), MemberType = typeof(PublishTestUtils))]
        public void NativeAot_hw_runs_with_cross_PackageReference_PublishAot_is_enabled(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (RuntimeInformation.OSArchitecture == Architecture.X64))
            {
                var projectName = "HellowWorldNativeAotApp";
                var rid = "win-arm64";

                var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "true";

                // This will add a reference to a package that will also be automatically imported by the SDK
                testProject.PackageReferences.Add(new TestPackageReference("Microsoft.DotNet.ILCompiler", ExplicitPackageVersion));
                testProject.PackageReferences.Add(new TestPackageReference("runtime.win-x64.Microsoft.DotNet.ILCompiler", ExplicitPackageVersion));

                var testAsset = _testAssetsManager.CreateTestProject(testProject);

                var publishCommand = new PublishCommand(testAsset);
                publishCommand
                    .Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                    .Should().Pass()
                // Having an explicit package reference will generate a warning
                .And.HaveStdOutContaining("warning")
                .And.HaveStdOutContaining("Microsoft.DotNet.ILCompiler");

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
                var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
                var publishedExe = Path.Combine(publishDirectory, $"{testProject.Name}{Constants.ExeSuffix}");
                File.Exists(publishedDll).Should().BeFalse();
                File.Exists(publishedExe).Should().BeTrue();

                CheckIlcVersions(testAsset, targetFramework, rid, ExplicitPackageVersion);
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_hw_runs_with_cross_PackageReference_PublishAot_is_empty(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (RuntimeInformation.OSArchitecture == Architecture.X64))
            {
                var projectName = "HellowWorldNativeAotApp";
                var rid = "win-arm64";

                var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);

                // This will add a reference to a package that will also be automatically imported by the SDK
                testProject.PackageReferences.Add(new TestPackageReference("Microsoft.DotNet.ILCompiler", ExplicitPackageVersion));
                testProject.PackageReferences.Add(new TestPackageReference("runtime.win-x64.Microsoft.DotNet.ILCompiler", ExplicitPackageVersion));

                var testAsset = _testAssetsManager.CreateTestProject(testProject);

                var publishCommand = new PublishCommand(testAsset);
                publishCommand
                    .Execute($"/p:RuntimeIdentifier={rid}")
                    .Should().Pass();

                // Not setting PublishAot to true will be a normal publish
                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
                var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
                File.Exists(publishedDll).Should().BeTrue();
            }
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void NativeAot_hw_fails_with_sdk6_PublishAot_is_enabled()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var projectName = "HellowWorldNativeAotApp";

                var testProject = CreateHelloWorldTestProject("net6.0", projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "true";

                var testAsset = _testAssetsManager.CreateTestProject(testProject);

                var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
                publishCommand
                    .Execute($"/p:UseCurrentRuntimeIdentifier=true", "/p:SelfContained=true")
                    .Should().Fail()
                    .And.HaveStdOutContaining("error NETSDK1207:");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_hw_fails_with_sdk6_PackageReference_PublishAot_is_enabled(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var projectName = "HellowWorldNativeAotApp";
                var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

                var testProject = CreateHelloWorldTestProject("net6.0", projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "true";

                testProject.PackageReferences.Add(new TestPackageReference("Microsoft.DotNet.ILCompiler", ExplicitPackageVersion));

                var testAsset = _testAssetsManager.CreateTestProject(testProject);

                var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
                publishCommand
                    .Execute($"/p:UseCurrentRuntimeIdentifier=true", "/p:SelfContained=true")
                    .Should().Fail()
                    .And.HaveStdOutContaining("error NETSDK1207:");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_hw_fails_with_unsupported_target_rid(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                var projectName = "HelloWorldUnsupportedTargetRid";
                var rid = "linux-x64";
                var unsupportedTargetRid = "linux-arm64";

                var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "true";

                var testAsset = _testAssetsManager.CreateTestProject(testProject)
                    .WithProjectChanges(project => OverrideKnownILCompilerPackRuntimeIdentifiers(project, $"{rid};"));

                var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
                publishCommand
                    .Execute($"/p:RuntimeIdentifier={unsupportedTargetRid}", "/p:SelfContained=true")
                    .Should().Fail()
                    .And.HaveStdOutContaining("error NETSDK1203:");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAot_hw_fails_with_unsupported_host_rid(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                var projectName = "HelloWorldUnsupportedHostRid";
                var supportedTargetRid = "linux-arm64";

                var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
                testProject.AdditionalProperties["PublishAot"] = "true";

                var testAsset = _testAssetsManager.CreateTestProject(testProject)
                    .WithProjectChanges(project => OverrideKnownILCompilerPackRuntimeIdentifiers(project, $"{supportedTargetRid};"));

                var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
                publishCommand
                    .Execute($"/p:RuntimeIdentifier={supportedTargetRid}", "/p:SelfContained=true")
                    .Should().Fail()
                    .And.HaveStdOutContaining("error NETSDK1204:");
            }
        }

        private void OverrideKnownILCompilerPackRuntimeIdentifiers(XDocument project, string runtimeIdentifiers)
        {
            var ns = project.Root.Name.Namespace;
            project.Root.Add(new XElement(ns + "ItemGroup",
                new XElement(ns + "KnownILCompilerPack",
                    new XAttribute("Update", "@(KnownILCompilerPack)"),
                    new XElement(ns + "ILCompilerRuntimeIdentifiers", runtimeIdentifiers))));
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void Only_Aot_warnings_are_produced_if_EnableAotAnalyzer_is_set(string targetFramework)
        {
            var projectName = "WarningAppWithAotAnalyzer";
            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, true);
            // Inactive linker/single-file analyzers should have no effect on the aot analyzer,
            // unless PublishAot is also set.
            testProject.AdditionalProperties["EnableAotAnalyzer"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute(RuntimeIdentifier)
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL3050")
                .And.HaveStdOutContaining("warning IL3056")
                .And.NotHaveStdOutContaining("warning IL2026")
                .And.NotHaveStdOutContaining("warning IL3002");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void IsAotCompatible_implies_enable_analyzers(string targetFramework)
        {
            var projectName = "WarningAppWithAotAnalyzer";
            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, true);
            testProject.AdditionalProperties["IsAotCompatible"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            buildCommand
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL3050")
                .And.HaveStdOutContaining("warning IL3056")
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL3002");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}.dll");

            // injects the IsTrimmable attribute
            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("IsTrimmable:True");

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute(RuntimeIdentifier, "/p:RunAnalyzers=false")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL3050")
                .And.NotHaveStdOutContaining("warning IL3056")
                .And.NotHaveStdOutContaining("warning IL2026")
                .And.NotHaveStdOutContaining("warning IL3002");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0", true)]
        [InlineData("net6.0", true)]
        [InlineData("net7.0", false)]
        public void PublishAot_fails_for_unsupported_target_framework(string targetFramework, bool shouldFail)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // OSX wasn't supported before net8
                return;
            }

            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = new TestProject()
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["PublishAot"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true");
            if (shouldFail) {
                result.Should().Fail()
                    .And.HaveStdOutContaining(Strings.AotUnsupportedTargetFramework);
            } else {
                result.Should().Pass()
                    .And.NotHaveStdOutContaining("warning");
            }
        }

        [RequiresMSBuildVersionTheory("17.8.0")]
        [InlineData("netstandard2.0", true)]
        [InlineData("net6.0", true)]
        [InlineData("net7.0", false)]
        [InlineData("netstandard2.0;net5.0", true)] // None of these TFMs are supported for AOT
        [InlineData("netstandard2.0;net7.0", false)] // Net7.0 is the min TFM supported for AOT and targeting.
        [InlineData("netstandard2.0;net8.0", true)] // Net8.0 is supported for AOT, but leaves a "gap" for the supported net7.0 TFMs.
        [InlineData("alias-ns2", true)]
        [InlineData("alias-n6", true)]
        [InlineData("alias-n7", false)]
        [InlineData("alias-n7;alias-n8", false)] // If all TFMs are supported, there's no warning even though the project uses aliases.
        [InlineData("alias-ns2;alias-n7", true)] // This is correctly multi-targeted, but the logic can't detect this due to the alias so it still warns.
        public void IsAotCompatible_warns_when_expected_for_not_correctly_multitarget_libraries(string targetFrameworks, bool shouldWarn)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFrameworks);

            var testProject = new TestProject()
            {
                Name = "ClassLibTest",
                TargetFrameworks = targetFrameworks
            };
            testProject.AdditionalProperties["IsAotCompatible"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFrameworks)
                .WithProjectChanges(AddTargetFrameworkAliases);

            var buildCommand = new BuildCommand(testAsset);
            var resultAssertion = buildCommand.Execute()
                .Should().Pass();
            if (shouldWarn) {
                resultAssertion
                    // Note: can't check for Strings.IsAotCompatibleUnsupported because each line of
                    // the message gets prefixed with a file path by MSBuild.
                    .And.HaveStdOutContaining($"warning NETSDK1210")
                    .And.HaveStdOutContaining($"<IsAotCompatible Condition=\"$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net7.0'))\">true</IsAotCompatible>");
            } else {
                resultAssertion.And.NotHaveStdOutContaining($"warning");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void Requires_analyzers_produce_warnings_without_PublishAot_being_set(string targetFramework)
        {
            var projectName = "WarningAppWithRequiresAnalyzers";

            // Enable the different requires analyzers (EnableAotAnalyzer, EnableTrimAnalyzer
            // and EnableSingleFileAnalyzer) without setting PublishAot
            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, true);
            testProject.AdditionalProperties["EnableAotAnalyzer"] = "true";
            testProject.AdditionalProperties["EnableTrimAnalyzer"] = "true";
            testProject.AdditionalProperties["EnableSingleFileAnalyzer"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL3050")
                .And.HaveStdOutContaining("warning IL3056")
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL3002");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net7Plus), MemberType = typeof(PublishTestUtils))]
        public void NativeAot_compiler_runs_when_PublishAot_is_enabled(string targetFramework)
        {
            if (targetFramework == "net7.0" && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // OSX wasn't supported before net8
                return;
            }

            var projectName = "WarningAppWithPublishAot";

            // PublishAot should enable the EnableAotAnalyzer, EnableTrimAnalyzer and EnableSingleFileAnalyzer
            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, true);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL3050")
                .And.HaveStdOutContaining("warning IL3056")
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL3002");

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid);

            var publishedExe = Path.Combine(publishDirectory.FullName, $"{testProject.Name}{Constants.ExeSuffix}");

            // The exe exist and should be native
            File.Exists(publishedExe).Should().BeTrue();
            IsNativeImage(publishedExe).Should().BeTrue();

            var command = new RunExeCommand(Log, publishedExe)
                .Execute().Should().Pass()
                .And.HaveStdOutContaining("Hello world");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void Warnings_are_generated_even_with_analyzers_disabled(string targetFramework)
        {

            var projectName = "WarningAppWithPublishAotAnalyzersDisabled";

            // PublishAot enables the EnableAotAnalyzer, EnableTrimAnalyzer and EnableSingleFileAnalyzer
            // only if they don't have a predefined value
            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, true);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["EnableAotAnalyzer"] = "false";
            testProject.AdditionalProperties["EnableTrimAnalyzer"] = "false";
            testProject.AdditionalProperties["EnableSingleFileAnalyzer"] = "false";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL3050")
                .And.HaveStdOutContaining("warning IL2026");

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid);

            var publishedExe = Path.Combine(publishDirectory.FullName, $"{testProject.Name}{Constants.ExeSuffix}");

            // The exe exist and should be native
            File.Exists(publishedExe).Should().BeTrue();
            IsNativeImage(publishedExe).Should().BeTrue();

            var command = new RunExeCommand(Log, publishedExe)
                .Execute().Should().Pass()
                .And.HaveStdOutContaining("Hello world");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAotStaticLib_only_runs_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "AotStaticLibraryPublish";

            var testProject = CreateTestProjectWithAotLibrary(targetFramework, projectName);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["NativeLib"] = "Static";
            testProject.SelfContained = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute()
                .Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var staticLibSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".lib" : ".a";
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}{staticLibSuffix}");

            // The lib exist and should be native
            File.Exists(publishedDll).Should().BeTrue();
            IsNativeImage(publishedDll).Should().BeTrue();
        }

        [Theory]
        [InlineData("Static")]
        [InlineData("Shared")]
        public void NativeAotLib_warns_when_eventpipe_is_enabled(string libType)
        {
            var projectName = "AotStaticLibraryPublishWithEventPipe";
            var rid = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework);

            var testProject = CreateTestProjectWithAotLibrary(ToolsetInfo.CurrentTargetFramework, projectName);
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = rid;
            testProject.AdditionalProperties["NativeLib"] = libType;
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["EventSourceSupport"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            // Revisit once the issue is fixed
            // https://github.com/dotnet/runtime/issues/89346
            publishCommand
                .Execute()
                .Should().Pass();
            // Comment in the following code when https://github.com/dotnet/sdk/issues/34839 gets fixed
            // .And.HaveStdOutContaining("EventSource is not supported or recommended when compiling to a native library");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void NativeAotSharedLib_only_runs_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "AotSharedLibraryPublish";

            var testProject = CreateTestProjectWithAotLibrary(targetFramework, projectName);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            testProject.AdditionalProperties["NativeLib"] = "Shared";
            testProject.AdditionalProperties["SelfContained"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute()
                .Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            var rid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var sharedLibSuffix = GetSharedLibSuffix();
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}{sharedLibSuffix}");

            // The lib exist and should be native
            File.Exists(publishedDll).Should().BeTrue();
            IsNativeImage(publishedDll).Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_publishes_with_implicit_rid_with_NativeAotApp(string targetFramework)
        {
            var projectName = "ImplicitRidNativeAotApp";
            var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
            testProject.AdditionalProperties["PublishAot"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new DotnetPublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute()
                .Should()
                .Pass();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_builds_with_dynamiccodesupport_false_when_publishaot_true(string targetFramework)
        {
            var projectName = "DynamicCodeSupportFalseApp";
            var testProject = CreateHelloWorldTestProject(targetFramework, projectName, true);
            testProject.AdditionalProperties["PublishAot"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            string outputDirectory = buildCommand.GetOutputDirectory(targetFramework: targetFramework).FullName;
            string runtimeConfigFile = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);

            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
            JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];
            configProperties["System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported"].Value<bool>()
                .Should().BeFalse();
        }

        private void GetKnownILCompilerPackVersion(TestAsset testAsset, string targetFramework, out string version)
        {
            var getKnownPacks = new GetValuesCommand(testAsset, "KnownILCompilerPack", GetValuesCommand.ValueType.Item, targetFramework)
            {
                MetadataNames = new List<string> { "TargetFramework", "ILCompilerPackVersion" }
            };
            getKnownPacks.Execute().Should().Pass();
            var knownPacks = getKnownPacks.GetValuesWithMetadata();
            version = knownPacks
                .Where(i => i.metadata["TargetFramework"] == targetFramework)
                .Select(i => i.metadata["ILCompilerPackVersion"])
                .Single();
        }

        private void CheckIlcVersions(TestAsset testAsset, string targetFramework, string rid, string expectedVersion)
        {
            // Compiler version matches expected version
            var ilcToolsPathCommand = new GetValuesCommand(testAsset, "IlcToolsPath", targetFramework: targetFramework)
            {
                DependsOnTargets = "WriteIlcRspFileForCompilation"
            };
            ilcToolsPathCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true").Should().Pass();
            var ilcToolsPath = ilcToolsPathCommand.GetValues()[0];
            var ilcVersion = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(ilcToolsPath)));
            ilcVersion.Should().Be(expectedVersion);

            // Compilation references (corelib) match expected version
            var ilcReferenceCommand = new GetValuesCommand(testAsset, "IlcReference", GetValuesCommand.ValueType.Item, targetFramework)
            {
                DependsOnTargets = "WriteIlcRspFileForCompilation"
            };
            ilcReferenceCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true").Should().Pass();
            var ilcReference = ilcReferenceCommand.GetValues();
            var corelibReference = ilcReference.Where(r => Path.GetFileName(r).Equals("System.Private.CoreLib.dll")).Single();
            var ilcReferenceVersion = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(corelibReference)));
            ilcReferenceVersion.Should().Be(expectedVersion);
        }

        private TestProject CreateHelloWorldTestProject(string targetFramework, string projectName, bool isExecutable)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = isExecutable
            };

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
class Test
{
    static void Main(String[] args)
    {
        Console.WriteLine(""Hello World"");
    }
}";

            return testProject;
        }

        private TestProject CreateAppForConfigCheck(string targetFramework, string projectName, bool isExecutable)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = isExecutable
            };

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
class Test
{
    static void Main(String[] args)
    {
        var config1 = AppContext.GetData(""key1"");

        string expected = ""value1"";

        if(!config1.Equals(expected))
            throw new ArgumentException($""Test failed, expected:<{expected}>, returned:<{config1}>"");
    }
}";

            return testProject;
        }

        private TestProject CreateTestProjectWithAnalysisWarnings(string targetFramework, string projectName, bool isExecutable)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = isExecutable
            };

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
class C
{
    static void Main()
    {
        ProduceAotAnalysisWarning();
        ProduceTrimAnalysisWarning();
        ProduceSingleFileAnalysisWarning();
        Console.WriteLine(""Hello world"");
    }

    [RequiresDynamicCode(""Aot analysis warning"")]
    static void ProduceAotAnalysisWarning()
    {
    }

    [RequiresDynamicCode(""Aot analysis warning"")]
    static C()
    {
    }

    [RequiresUnreferencedCode(""Trim analysis warning"")]
    static void ProduceTrimAnalysisWarning()
    {
    }

    [RequiresAssemblyFiles(""Single File analysis warning"")]
    static void ProduceSingleFileAnalysisWarning()
    {
    }
}";

            return testProject;
        }

        private static string GetSharedLibSuffix()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ".dll";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return ".so";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return ".dylib";
            }
            throw new PlatformNotSupportedException();
        }

        private static bool DoSymbolsExist(string baseDir, string baseName)
        {
            string suffix;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                suffix = ".pdb";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                suffix = ".dbg";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                suffix = ".dsym";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            var path = Path.Combine(baseDir, baseName + suffix);
            // Symbol file is a directory on OSX
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Directory.Exists(path)
                : File.Exists(path);
        }

        private TestProject CreateTestProjectWithAotLibrary(string targetFramework, string projectName)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework
            };

            testProject.SourceFiles[$"{projectName}.cs"] = @"
public class NativeLibraryClass
{
    public void LibraryMethod()
    {
    }
}";
            return testProject;
        }

        private static bool IsNativeImage(string path)
        {
            try
            {
                using (FileStream fs = new(path, FileMode.Open, FileAccess.Read))
                using (var peReader = new PEReader(fs))
                {
                    return !peReader.HasMetadata;
                }
            }
            catch (BadImageFormatException)
            {
                return true;
            }
        }

        private void AddRuntimeConfigOption(XDocument project)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Add(new XElement(ns + "ItemGroup",
                                new XElement("RuntimeHostConfigurationOption",
                                    new XAttribute("Include", "key1"),
                                    new XAttribute("Value", "value1"))));
        }
    }
}
