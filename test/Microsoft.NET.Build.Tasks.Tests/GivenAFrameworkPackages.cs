// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.ComponentDetection.Detectors.NuGet;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Direct unit tests for <see cref="FrameworkPackages.LoadFrameworkPackagesFromPack"/>, which after the
    /// multithreaded-task migration takes an <see cref="AbsolutePath"/> for the targeting pack root. These
    /// exercise the method in isolation (the early-out guards, the missing-folder branch, and a successful
    /// PackageOverrides.txt load) rather than only through <see cref="GetPackagesToPrune"/>.
    /// </summary>
    public class GivenAFrameworkPackages
    {
        private const string NetCoreApp = "Microsoft.NETCore.App";

        private sealed class TestLogger : Logger
        {
            protected override void LogCore(in Message message) { }
        }

        private static NuGetFramework Net10 => new NuGetFramework(".NETCoreApp", new Version(10, 0, 0));

        private static AbsolutePath AbsolutePathFor(string path) =>
            TaskEnvironmentHelper.CreateForTest(Path.GetTempPath()).GetAbsolutePath(path);

        [Fact]
        public void ItReturnsNullWhenFrameworkIsNotANetCoreAppPack()
        {
            // null framework and non-.NETCoreApp frameworks both short-circuit to null.
            FrameworkPackages.LoadFrameworkPackagesFromPack(new TestLogger(), framework: null, NetCoreApp, AbsolutePathFor(Path.GetTempPath()))
                .Should().BeNull("a null framework is not a .NETCoreApp targeting pack");

            var netFramework = new NuGetFramework(".NETFramework", new Version(4, 7, 2));
            FrameworkPackages.LoadFrameworkPackagesFromPack(new TestLogger(), netFramework, NetCoreApp, AbsolutePathFor(Path.GetTempPath()))
                .Should().BeNull("only .NETCoreApp frameworks have targeting pack data");
        }

        [Fact]
        public void ItReturnsNullWhenTargetingPackFolderDoesNotExist()
        {
            var missingRoot = Path.Combine(Path.GetTempPath(), "fp-missing-" + Guid.NewGuid().ToString("N"));

            FrameworkPackages.LoadFrameworkPackagesFromPack(new TestLogger(), Net10, NetCoreApp, AbsolutePathFor(missingRoot))
                .Should().BeNull("a non-existent targeting pack folder yields no packages");
        }

        [Fact]
        public void ItLoadsPackageOverridesFromTargetingPack()
        {
            var root = Path.Combine(Path.GetTempPath(), "fp-" + Guid.NewGuid().ToString("N"));
            try
            {
                // <root>/Microsoft.NETCore.App.Ref/10.0.0/data/PackageOverrides.txt
                var overridesFile = Path.Combine(root, NetCoreApp + ".Ref", "10.0.0", "data", "PackageOverrides.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(overridesFile)!);
                File.WriteAllText(overridesFile, "Newtonsoft.Json|13.0.1");

                var result = FrameworkPackages.LoadFrameworkPackagesFromPack(new TestLogger(), Net10, NetCoreApp, AbsolutePathFor(root));

                result.Should().NotBeNull("a PackageOverrides.txt exists under the targeting pack root");
                result.Packages.Should().ContainKey("Newtonsoft.Json");
                result.Packages["Newtonsoft.Json"].ToString().Should().Be("13.0.1");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
