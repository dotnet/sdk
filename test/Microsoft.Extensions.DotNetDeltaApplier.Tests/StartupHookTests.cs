// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.DotNetDeltaApplier
{
    public class StartupHookTests
    {
        [Fact]
        public void ClearHotReloadEnvironmentVariables_ClearsStartupHook()
        {
            Assert.Equal("", StartupHook.RemoveCurrentAssembly(typeof(StartupHook).Assembly.Location));
        }

        [Fact]
        public void ClearHotReloadEnvironmentVariables_PreservedOtherStartupHooks()
        {
            var customStartupHook = "/path/mycoolstartup.dll";
            Assert.Equal(customStartupHook, StartupHook.RemoveCurrentAssembly(typeof(StartupHook).Assembly.Location + Path.PathSeparator + customStartupHook));
        }

        [Fact]
        public void ClearHotReloadEnvironmentVariables_RemovesHotReloadStartup_InCaseInvariantManner()
        {
            var customStartupHook = "/path/mycoolstartup.dll";
            Assert.Equal(customStartupHook, StartupHook.RemoveCurrentAssembly(customStartupHook + Path.PathSeparator + typeof(StartupHook).Assembly.Location.ToUpperInvariant()));
        }

        [Theory]
        [CombinatorialData]
        public void IsMatchingProcess_Matching_SimpleName(
            [CombinatorialValues("", ".dll", ".exe")] string extension,
            [CombinatorialValues("", ".dll", ".exe")] string targetExtension)
        {
            var dir = Path.GetDirectoryName(typeof(StartupHookTests).Assembly.Location)!;
            var name = "a";
            var processPath = Path.Combine(dir, name + extension);
            var targetProcessPath = Path.Combine(dir, "a" + targetExtension);

            Assert.True(StartupHook.IsMatchingProcess(processPath, targetProcessPath));
        }

        [Theory]
        [CombinatorialData]
        public void IsMatchingProcess_Matching_DotInName(
            [CombinatorialValues("", ".dll", ".exe")] string extension,
            [CombinatorialValues("", ".dll", ".exe")] string targetExtension)
        {
            var dir = Path.GetDirectoryName(typeof(StartupHookTests).Assembly.Location)!;
            var name = "a.b";
            var processPath = Path.Combine(dir, name + extension);
            var targetProcessPath = Path.Combine(dir, name + targetExtension);

            Assert.True(StartupHook.IsMatchingProcess(processPath, targetProcessPath));
        }

        [Theory]
        [CombinatorialData]
        public void IsMatchingProcess_Matching_DotDllInName(
            [CombinatorialValues("", ".dll", ".exe")] string extension,
            [CombinatorialValues("", ".dll", ".exe")] string targetExtension)
        {
            var dir = Path.GetDirectoryName(typeof(StartupHookTests).Assembly.Location)!;
            var name = "a.dll";
            var processPath = Path.Combine(dir, name + extension);
            var targetProcessPath = Path.Combine(dir, name + targetExtension);

            Assert.True(StartupHook.IsMatchingProcess(processPath, targetProcessPath));
        }

        [Theory]
        [CombinatorialData]
        public void IsMatchingProcess_NotMatching(
            [CombinatorialValues("", ".dll", ".exe")] string extension,
            [CombinatorialValues("", ".dll", ".exe")] string targetExtension)
        {
            var dir = Path.GetDirectoryName(typeof(StartupHookTests).Assembly.Location)!;
            var processPath = Path.Combine(dir, "a" + extension);
            var targetProcessPath = Path.Combine(dir, "b" + targetExtension);

            Assert.False(StartupHook.IsMatchingProcess(processPath, targetProcessPath));
        }
    }
}
