// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class StartupHookTests
    {
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
