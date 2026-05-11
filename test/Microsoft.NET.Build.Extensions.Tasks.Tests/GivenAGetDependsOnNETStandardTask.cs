// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetDependsOnNETStandardTask(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void CanCheckThisAssembly()
        {
            var thisAssemblyPath = typeof(GivenAGetDependsOnNETStandardTask).GetTypeInfo().Assembly.Location;
            var task = new GetDependsOnNETStandard()
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                References = new[] { new MockTaskItem() { ItemSpec = thisAssemblyPath } }
            };
            task.Execute().Should().BeTrue();

            // this test compiles against a sufficiently high System.Runtime
            task.DependsOnNETStandard.Should().BeTrue();
        }

        [Fact]
        public void CanCheckThisAssemblyByHintPath()
        {
            var thisAssemblyPath = typeof(GivenAGetDependsOnNETStandardTask).GetTypeInfo().Assembly.Location;
            var task = new GetDependsOnNETStandard()
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                References = new[]
                {
                    new MockTaskItem(
                        Path.GetFileNameWithoutExtension(thisAssemblyPath),
                        new Dictionary<string, string>
                        {
                            {"HintPath", thisAssemblyPath }
                        })
                }
            };
            task.Execute().Should().BeTrue();

            // this test compiles against a sufficiently high System.Runtime
            task.DependsOnNETStandard.Should().BeTrue();
        }

        [Fact]
        public void ReturnsFalseForNonPE()
        {
            string testFile = $"testFile.{nameof(GivenAGetDependsOnNETStandardTask)}.{nameof(ReturnsFalseForNonPE)}.txt";
            try
            {
                File.WriteAllText(testFile, "test file");
                var task = new GetDependsOnNETStandard()
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    References = new[] { new MockTaskItem() { ItemSpec = testFile } }
                };
                // ensure that false is returned and no exception is thrown
                task.Execute().Should().BeTrue();
                task.DependsOnNETStandard.Should().BeFalse();
                // warn for a non-PE file
                ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().BeGreaterThan(0);
            }
            finally
            {
                File.Delete(testFile);
            }
        }

        [Fact]
        public void ReturnsFalseForNativeLibrary()
        {
            var corelibLocation = typeof(object).GetTypeInfo().Assembly.Location;
            var corelibFolder = Path.GetDirectoryName(corelibLocation);

            // do our best to try and find it, intentionally only use the windows name since linux will not be a PE.
            var coreclrLocation = Directory.EnumerateFiles(corelibFolder, "coreclr.dll").FirstOrDefault();

            // if we can't find it, skip the test
            if (coreclrLocation != null)
            {
                // ensure that false is returned and no warning is logged for native library
                var task = new GetDependsOnNETStandard()
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    References = new[] { new MockTaskItem() { ItemSpec = coreclrLocation } }
                };

                task.Execute().Should().BeTrue();
                task.DependsOnNETStandard.Should().BeFalse();
                // don't warn for PE with no metadata
                ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().Be(0);
            }
        }

        [Fact]
        public void SucceedsOnMissingFileReturnsFalse()
        {
            var missingFile = $"{nameof(SucceedsOnMissingFileReturnsFalse)}.shouldNotExist.dll";
            File.Exists(missingFile).Should().BeFalse();
            var task = new GetDependsOnNETStandard()
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                References = new[] { new MockTaskItem() { ItemSpec = missingFile } }
            };

            task.Execute().Should().BeTrue();
            task.DependsOnNETStandard.Should().BeFalse();
            ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().Be(0);
        }

        [Fact]
        public void SucceedsWithWarningOnLockedFile()
        {
            var testDir = TestAssetsManager.CreateTestDirectory();
            var lockedFileName = $"{nameof(SucceedsWithWarningOnLockedFile)}.dll";
            var lockedFilePath = Path.Combine(testDir.Path, lockedFileName);

            // Create file with exclusive lock (no sharing)
            using (var fileHandle = new FileStream(lockedFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                // Verify the lock is actually held before running the task.
                // On some CI machines, antimalware or file system filters can
                // interfere with file locking, causing the test to be unreliable.
                try
                {
                    using var probe = new FileStream(lockedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    Assert.Fail(
                        "File lock is not being enforced — the probe open should have thrown IOException. " +
                        "This may indicate antimalware or file system filter interference on this machine.");
                }
                catch (IOException)
                {
                    // Expected — the lock is working correctly
                }

                var task = new GetDependsOnNETStandard()
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(testDir.Path),
                    References = new[] { new MockTaskItem() { ItemSpec = lockedFileName } }
                };

                task.Execute().Should().BeTrue();
                task.DependsOnNETStandard.Should().BeFalse();
                ((MockBuildEngine)task.BuildEngine).Warnings.Count.Should().BeGreaterThan(0);
            }
        }
    }
}
