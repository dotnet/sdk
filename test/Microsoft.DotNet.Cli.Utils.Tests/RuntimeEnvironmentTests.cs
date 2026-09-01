// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    [TestClass]
    public class RuntimeEnvironmentTests : SdkTest
    {
        public RuntimeEnvironmentTests()
        {
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void VerifyWindows()
        {
            Assert.AreEqual(Platform.Windows, RuntimeEnvironment.OperatingSystemPlatform);
            Assert.AreEqual("Windows", RuntimeEnvironment.OperatingSystem);

            Version osVersion = Version.Parse(RuntimeEnvironment.OperatingSystemVersion);
            Version expectedOSVersion = Environment.OSVersion.Version;

            // 3 parts of the version should be supplied for Windows
            Assert.AreEqual(expectedOSVersion.Major, osVersion.Major);
            Assert.AreEqual(expectedOSVersion.Minor, osVersion.Minor);
            Assert.AreEqual(expectedOSVersion.Build, osVersion.Build);
            Assert.AreEqual(-1, osVersion.Revision);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.OSX)]
        public void VerifyMacOs()
        {
            Assert.AreEqual(Platform.Darwin, RuntimeEnvironment.OperatingSystemPlatform);
            Assert.AreEqual("Mac OS X", RuntimeEnvironment.OperatingSystem);

            Version osVersion = Version.Parse(RuntimeEnvironment.OperatingSystemVersion);
            Version expectedOSVersion = Environment.OSVersion.Version;

            // 2 parts of the version should be supplied for macOS
            Assert.AreEqual(expectedOSVersion.Major, osVersion.Major);
            Assert.AreEqual(expectedOSVersion.Minor, osVersion.Minor);
            Assert.AreEqual(-1, osVersion.Build);
            Assert.AreEqual(-1, osVersion.Revision);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Linux)]
        public void VerifyLinux()
        {
            Assert.AreEqual(Platform.Linux, RuntimeEnvironment.OperatingSystemPlatform);

            var osRelease = File.ReadAllLines("/etc/os-release");
            string id = osRelease
                .First(line => line.StartsWith("ID=", StringComparison.OrdinalIgnoreCase))
                .Substring("ID=".Length)
                .Trim('\"', '\'')
                .ToLowerInvariant();
            Assert.AreEqual(id, RuntimeEnvironment.OperatingSystem.ToLowerInvariant());

            string version = osRelease
                .First(line => line.StartsWith("VERSION_ID=", StringComparison.OrdinalIgnoreCase))
                .Substring("VERSION_ID=".Length)
                .Trim('\"', '\'');
            Assert.AreEqual(version, RuntimeEnvironment.OperatingSystemVersion);
        }
    }
}
