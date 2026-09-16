// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class ScopedLockFileTests : SdkTest
{
    [TestMethod]
    public void SharedOpenCreatesPermanentEmptyFileCoexistsAndDeniesExclusive()
    {
        var directory = Directory.CreateTempSubdirectory("scoped-lock-");
        try
        {
            var path = Path.Combine(directory.FullName, "activity.lock");
            using (var first = ScopedLockFile.TryAcquireShared(path))
            using (var second = ScopedLockFile.TryAcquireShared(path))
            {
                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                using var exclusive = ScopedLockFile.TryAcquireExclusive(path);
                Assert.IsNull(exclusive, "An exclusive lock must not be acquired while shared locks are held.");
            }

            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(0L, new FileInfo(path).Length);
            using var reacquired = ScopedLockFile.TryAcquireExclusive(path);
            Assert.IsNotNull(reacquired);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void ExclusiveDeniesBothModesAndDisposeIsIdempotent()
    {
        var directory = Directory.CreateTempSubdirectory("scoped-lock-");
        try
        {
            var path = Path.Combine(directory.FullName, "update.lock");
            using var lease = ScopedLockFile.TryAcquireExclusive(path);
            Assert.IsNotNull(lease);
            using var shared = ScopedLockFile.TryAcquireShared(path);
            using var exclusive = ScopedLockFile.TryAcquireExclusive(path);
            Assert.IsNull(shared);
            Assert.IsNull(exclusive);
            lease.Dispose();
            lease.Dispose();
            using var reacquired = ScopedLockFile.TryAcquireShared(path);
            Assert.IsNotNull(reacquired, "The existing permanent lock file must remain reusable after releasing its lock.");
            Assert.AreEqual(0L, new FileInfo(path).Length);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void MissingDirectoryIsNotContention()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "activity.lock");
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => ScopedLockFile.TryAcquireShared(path));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => ScopedLockFile.TryAcquireExclusive(path));
    }

    [TestMethod]
    public void DirectoryAndInvalidPathErrorsAreNotContention()
    {
        var directory = Directory.CreateTempSubdirectory("scoped-lock-");
        try
        {
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => ScopedLockFile.TryAcquireShared(directory.FullName));
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => ScopedLockFile.TryAcquireExclusive(directory.FullName));
            Assert.ThrowsExactly<ArgumentException>(() => ScopedLockFile.TryAcquireShared("\0"));
            Assert.ThrowsExactly<ArgumentException>(() => ScopedLockFile.TryAcquireExclusive("\0"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}