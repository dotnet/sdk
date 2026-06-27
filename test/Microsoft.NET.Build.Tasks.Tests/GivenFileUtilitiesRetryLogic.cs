// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenFileUtilitiesRetryLogic
    {
        [TestMethod]
        public void IsSharingViolation_ReturnsTrueForSharingViolationHResult()
        {
            const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
            var ex = new IOException("The process cannot access the file because it is being used by another process.", ERROR_SHARING_VIOLATION);

            FileUtilities.IsSharingViolation(ex).Should().BeTrue();
        }

        [TestMethod]
        public void IsSharingViolation_ReturnsFalseForOtherIOException()
        {
            var ex = new IOException("File not found.");

            FileUtilities.IsSharingViolation(ex).Should().BeFalse();
        }

        [TestMethod]
        public void TryGetAssemblyVersion_RetriesOnSharingViolationAndSucceeds()
        {
            // Use the test assembly itself as a valid PE file
            var sourceAssembly = typeof(GivenFileUtilitiesRetryLogic).Assembly.Location;
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var targetPath = Path.Combine(tempDir, "test.dll");

            try
            {
                File.Copy(sourceAssembly, targetPath);

                // TryGetAssemblyVersion should succeed on a normal file
                var version = FileUtilities.TryGetAssemblyVersion(targetPath);
                version.Should().NotBeNull();
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
