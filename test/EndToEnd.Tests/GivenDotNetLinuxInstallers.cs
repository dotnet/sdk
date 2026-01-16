// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests
{
    public class GivenDotNetLinuxInstallers(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void ItHasExpectedDependencies() =>
            ValidatePackage(
                debValidation: DebianPackageHasDependencyOnAspNetCoreStoreAndDotnetRuntime,
                rpmValidation: RpmPackageHasDependencyOnAspNetCoreStoreAndDotnetRuntime);

        [Fact]
        public void ItPreservesSymbolicLinksInPackage() =>
            ValidatePackage(
                debValidation: DebianPackageHasRelativeSymbolicLinks,
                rpmValidation: RpmPackageHasRelativeSymbolicLinks);

        private void ValidatePackage(Action<string> debValidation, Action<string> rpmValidation)
        {
            var installerFile = Environment.GetEnvironmentVariable("SDK_INSTALLER_FILE");
            if (string.IsNullOrEmpty(installerFile))
            {
                return;
            }

            var ext = Path.GetExtension(installerFile);
            switch (ext)
            {
                case ".deb":
                    debValidation(installerFile);
                    return;
                case ".rpm":
                    rpmValidation(installerFile);
                    return;
            }
        }

        private void DebianPackageHasDependencyOnAspNetCoreStoreAndDotnetRuntime(string installerFile) =>
            // Example output:

            // $ dpkg --info dotnet-sdk-2.1.105-ubuntu-x64.deb

            // new debian package, version 2.0.
            // size 75660448 bytes: control archive=29107 bytes.
            //     717 bytes,    11 lines      control
            // 123707 bytes,  1004 lines      md5sums
            //     1710 bytes,    28 lines   *  postinst             #!/usr/bin/env
            // Package: dotnet-sdk-2.1.104
            // Version: 2.1.104-1
            // Architecture: amd64
            // Maintainer: Microsoft <dotnetcore@microsoft.com>
            // Installed-Size: 201119
            // Depends: dotnet-runtime-2.0.6, aspnetcore-store-2.0.6
            // Section: devel
            // Priority: standard
            // Homepage: https://dotnet.github.io/core
            // Description: Microsoft .NET Core SDK - 2.1.104

            new RunExeCommand(Log, "dpkg")
                .Execute("--info", installerFile)
                .Should().Pass()
                    .And.HaveStdOutMatching(@"Depends:.*\s?dotnet-runtime-\d+(\.\d+){2}")
                    .And.HaveStdOutMatching(@"Depends:.*\s?aspnetcore-store-\d+(\.\d+){2}");

        private void RpmPackageHasDependencyOnAspNetCoreStoreAndDotnetRuntime(string installerFile) =>
            // Example output:

            // $ rpm -qpR dotnet-sdk-2.1.105-rhel-x64.rpm

            // dotnet-runtime-2.0.7 >= 2.0.7
            // aspnetcore-store-2.0.7 >= 2.0.7
            // /bin/sh
            // /bin/sh
            // rpmlib(PayloadFilesHavePrefix) <= 4.0-1
            // rpmlib(CompressedFileNames) <= 3.0.4-1

            new RunExeCommand(Log, "rpm")
                .Execute("-qpR", installerFile)
                .Should().Pass()
                    .And.HaveStdOutMatching(@"dotnet-runtime-\d+(\.\d+){2} >= \d+(\.\d+){2}")
                    .And.HaveStdOutMatching(@"aspnetcore-store-\d+(\.\d+){2} >= \d+(\.\d+){2}");

        private void DebianPackageHasRelativeSymbolicLinks(string installerFile) =>
            SymbolicLinkHelpers.VerifyPackageSymlinks(installerFile, "deb", tempDir =>
            {
                // Extract .deb archive (contains data.tar.gz)
                new RunExeCommand(Log, "ar")
                    .WithWorkingDirectory(tempDir)
                    .Execute("x", installerFile)
                    .Should().Pass();

                // Extract data.tar.gz to get the actual files
                var dataTarGz = Path.Combine(tempDir, "data.tar.gz");
                SymbolicLinkHelpers.ExtractTarGz(dataTarGz, tempDir, Log);
            }, Log);

        private void RpmPackageHasRelativeSymbolicLinks(string installerFile) =>
            SymbolicLinkHelpers.VerifyPackageSymlinks(installerFile, "rpm", tempDir =>
            {
                // Extract RPM contents using rpm2cpio | cpio (available on Azure Linux)
                new RunExeCommand(Log, "sh")
                    .WithWorkingDirectory(tempDir)
                    .Execute("-c", $"rpm2cpio '{installerFile}' | cpio -idv --quiet 2>&1")
                    .Should().Pass();
            }, Log);
    }
}
