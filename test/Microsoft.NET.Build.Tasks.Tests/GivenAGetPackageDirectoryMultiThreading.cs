// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetPackageDirectoryMultiThreading
    {
        // Sin 1 (Output property contamination): when the caller passes a relative PackageFolders
        // entry, the migration must NOT leak the TaskEnvironment-absolutized form into the
        // [Output] items' PackageDirectory metadata. The relative prefix is substituted back into
        // NuGet's absolutized result via string surgery, so this exercises that surgery across a
        // range of folder shapes (nested, forward slashes, trailing separators).
        [Theory]
        [InlineData("packages")]               // flat relative folder
        [InlineData("nested/packages")]        // nested with forward slash
        [InlineData("a/b/c/packages")]         // deeply nested
        [InlineData("packages/")]              // trailing separator
        public void PackageDirectoryMetadata_PreservesRelativeFolderShape_WhenInputIsRelative(string relativeFolder)
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "gpd-rel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var absolutePackagesDir = Path.Combine(projectDir, relativeFolder);
                CreateFakeNuGetPackage(absolutePackagesDir, "Newtonsoft.Json", "13.0.1");

                var item = new TaskItem("Newtonsoft.Json");
                item.SetMetadata(MetadataKeys.NuGetPackageId, "Newtonsoft.Json");
                item.SetMetadata(MetadataKeys.NuGetPackageVersion, "13.0.1");

                var task = new GetPackageDirectory
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    Items = new ITaskItem[] { item },
                    // Production folders are usually absolute; this test deliberately uses a relative
                    // one to verify a relative input still yields relative PackageDirectory metadata.
                    PackageFolders = new[] { relativeFolder }
                };

                task.Execute().Should().BeTrue("task should locate the package via the absolutized folder");

                task.Output.Should().HaveCount(1);
                var resolved = task.Output[0].GetMetadata(MetadataKeys.PackageDirectory);

                resolved.Should().NotBeNullOrEmpty("a package directory should be resolved");
                Path.IsPathRooted(resolved).Should().BeFalse(
                    "PackageDirectory must preserve the caller's relative shape; absolutization must not leak into [Output]");
                resolved.Should().StartWith(relativeFolder,
                    "the original relative prefix should be substituted back into the result");
                resolved.Should().NotContain(projectDir,
                    "TaskEnvironment.ProjectDirectory must not leak into PackageDirectory metadata");

                // Strongest check on the string surgery: re-rooting the returned relative path at the
                // project directory must point at the exact location NuGet resolved the package to,
                // regardless of separator/nesting differences in the substituted prefix.
                Path.GetFullPath(Path.Combine(projectDir, resolved))
                    .Should().Be(
                        Path.GetFullPath(Path.Combine(absolutePackagesDir, "newtonsoft.json", "13.0.1")),
                        "the rewritten relative path must resolve to the same physical package directory NuGet found");
            }
            finally
            {
                try { Directory.Delete(projectDir, true); } catch { }
            }
        }

        // Sin 6 (Exception type change): the Option-A pass-through for empty PackageFolders
        // entries must keep GetAbsolutePath from throwing ArgumentException. Specifically: an
        // empty user-package-folder is tolerated by NuGet's VersionFolderPathResolver (it stores
        // the empty string and probes harmlessly fail), so the task must still reach NuGet and
        // resolve packages from the remaining (valid) fallback folders.
        [Fact]
        public void EmptyPackageFolderEntry_DoesNotThrow_AndValidFallbackResolves()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "gpd-empty-" + Guid.NewGuid().ToString("N"));
            var packagesDir = Path.Combine(Path.GetTempPath(), "gpd-empty-pkgs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                CreateFakeNuGetPackage(packagesDir, "Newtonsoft.Json", "13.0.1");

                var item = new TaskItem("Newtonsoft.Json");
                item.SetMetadata(MetadataKeys.NuGetPackageId, "Newtonsoft.Json");
                item.SetMetadata(MetadataKeys.NuGetPackageVersion, "13.0.1");

                var task = new GetPackageDirectory
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    Items = new ITaskItem[] { item },
                    // Empty user folder + valid absolute fallback. Without Option-A pass-through,
                    // GetAbsolutePath("") would throw ArgumentException and the task would never
                    // reach NuGet. With pass-through, NuGet's VersionFolderPathResolver tolerates
                    // the empty user folder and the absolute fallback still resolves the package.
                    PackageFolders = new[] { string.Empty, packagesDir }
                };

                Action act = () => task.Execute();

                act.Should().NotThrow("empty PackageFolders entries must pass through GetAbsolutePath without raising ArgumentException");
                task.Output.Should().HaveCount(1);
                task.Output[0].GetMetadata(MetadataKeys.PackageDirectory)
                    .Should().StartWith(packagesDir, "the valid fallback folder should still resolve the package");
            }
            finally
            {
                try { Directory.Delete(projectDir, true); } catch { }
                try { Directory.Delete(packagesDir, true); } catch { }
            }
        }

        // Happy-path regression guard: when all PackageFolders are already absolute (the realistic
        // production case), the migration must produce the same absolute PackageDirectory metadata
        // it would have pre-migration, and must NOT root anything against TaskEnvironment.ProjectDirectory.
        [Fact]
        public void AbsolutePackageFolders_ProduceAbsoluteMetadata_AndDoNotLeakProjectDirectory()
        {
            var packagesDir = Path.Combine(Path.GetTempPath(), "gpd-abs-pkgs-" + Guid.NewGuid().ToString("N"));
            var unrelatedDir = Path.Combine(Path.GetTempPath(), "gpd-abs-unrelated-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(unrelatedDir);
            try
            {
                CreateFakeNuGetPackage(packagesDir, "Newtonsoft.Json", "13.0.1");

                var item = new TaskItem("Newtonsoft.Json");
                item.SetMetadata(MetadataKeys.NuGetPackageId, "Newtonsoft.Json");
                item.SetMetadata(MetadataKeys.NuGetPackageVersion, "13.0.1");

                var task = new GetPackageDirectory
                {
                    BuildEngine = new MockBuildEngine(),
                    // TaskEnvironment points at a completely unrelated directory; its ProjectDirectory
                    // must not appear anywhere in the output.
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(unrelatedDir),
                    Items = new ITaskItem[] { item },
                    PackageFolders = new[] { packagesDir }
                };

                task.Execute().Should().BeTrue();

                task.Output.Should().HaveCount(1);
                var resolved = task.Output[0].GetMetadata(MetadataKeys.PackageDirectory);

                resolved.Should().StartWith(packagesDir,
                    "PackageDirectory should reflect NuGet's resolved install path under the supplied folder");
                resolved.Should().NotContain(unrelatedDir,
                    "TaskEnvironment.ProjectDirectory must never appear in PackageDirectory metadata");
            }
            finally
            {
                try { Directory.Delete(packagesDir, true); } catch { }
                try { Directory.Delete(unrelatedDir, true); } catch { }
            }
        }

        // Creates the minimum on-disk layout FallbackPackagePathResolver requires to find a package:
        //   {root}/{id-lower}/{version}/{id-lower}.{version}.nupkg.sha512
        //   {root}/{id-lower}/{version}/{id-lower}.nuspec
        private static void CreateFakeNuGetPackage(string root, string id, string version)
        {
            var idLower = id.ToLowerInvariant();
            var pkgDir = Path.Combine(root, idLower, version);
            Directory.CreateDirectory(pkgDir);

            File.WriteAllText(
                Path.Combine(pkgDir, $"{idLower}.{version}.nupkg.sha512"),
                "abc123");
            File.WriteAllText(
                Path.Combine(pkgDir, $"{idLower}.nuspec"),
                $"<package><metadata><id>{id}</id><version>{version}</version></metadata></package>");
        }
    }
}
