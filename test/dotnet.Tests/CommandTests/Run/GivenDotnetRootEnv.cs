// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRootEnv : SdkTest
    {
        private static Version Version6_0 = new(6, 0);

        public GivenDotnetRootEnv(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ItShouldSetDotnetRootToDirectoryOfMuxer(string targetFramework)
        {
            string expectDotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            string processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
            string expectOutput = $"DOTNET_ROOT='';DOTNET_ROOT(x86)='';DOTNET_ROOT_{processArchitecture}='{expectDotnetRoot}'";


            var projectRoot = SetupDotnetRootEchoProject(null, targetFramework);

            var runCommand = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectRoot);

            runCommand.EnvironmentToRemove.Add("DOTNET_ROOT");
            runCommand.EnvironmentToRemove.Add("DOTNET_ROOT(x86)");

            runCommand.Execute("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(expectOutput);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Failed to load /private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), '/System/Volumes/Preboot/Cryptexes/OS/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (no such file), '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64'))
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [CombinatorialData]
        public void WhenDotnetRootIsSetItShouldSetDotnetRootToDirectoryOfMuxer(bool overrideDotnetRootArch)
        {
            string expectDotnetRoot = "OVERRIDE VALUE";

            var projectRoot = SetupDotnetRootEchoProject();
            var processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
            var runCommand = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectRoot);

            if (Environment.Is64BitProcess)
            {
                runCommand = runCommand.WithEnvironmentVariable(overrideDotnetRootArch ? "DOTNET_ROOT_X64" : "DOTNET_ROOT", expectDotnetRoot);
                runCommand.EnvironmentToRemove.Add("DOTNET_ROOT(x86)");
            }
            else
            {
                runCommand = runCommand.WithEnvironmentVariable(overrideDotnetRootArch ? "DOTNET_ROOT_X86" : "DOTNET_ROOT(x86)", expectDotnetRoot);
                runCommand.EnvironmentToRemove.Add("DOTNET_ROOT");
            }

            var expectedDotnetRoot = Environment.Is64BitProcess && overrideDotnetRootArch ? expectDotnetRoot : string.Empty;
            var expectedDotnetRootX86 = !Environment.Is64BitProcess && !overrideDotnetRootArch ? expectDotnetRoot : string.Empty;
            var expectedDotnetRootArch = overrideDotnetRootArch ? expectDotnetRoot : string.Empty;
            var expectedOutput = $"DOTNET_ROOT='{expectDotnetRoot}';DOTNET_ROOT(x86)='{expectedDotnetRootX86}';DOTNET_ROOT_{processArchitecture}='{expectedDotnetRootArch}'";

            runCommand.EnvironmentToRemove.Add($"DOTNET_ROOT_{processArchitecture}");
            runCommand
                .Execute("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(expectedOutput);
        }

        private string SetupDotnetRootEchoProject([CallerMemberName] string callingMethod = null, string targetFramework = null)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppEchoDotnetRoot", callingMethod, allowCopyIfPresent: true)
                .WithSource()
                .WithTargetFrameworkOrFrameworks(targetFramework ?? null, false)
                .Restore(Log);

            new BuildCommand(testAsset)
                .Execute($"{(!string.IsNullOrEmpty(targetFramework) ? "/p:TargetFramework=" + targetFramework : string.Empty)}")
                .Should()
                .Pass();

            return testAsset.Path;
        }
    }
}
