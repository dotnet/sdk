// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using EndToEnd.Tests.Utilities;

namespace EndToEnd.Tests
{
    public class GivenDotNetMacInstallers(ITestOutputHelper log) : SdkTest(log)
    {
        [Fact]
        public void ItPreservesSymbolicLinksInPackage()
        {
            var installerFile = Environment.GetEnvironmentVariable("SDK_INSTALLER_FILE");
            if (string.IsNullOrEmpty(installerFile))
            {
                return;
            }

            var ext = Path.GetExtension(installerFile);
            if (ext != ".pkg")
            {
                return;
            }

            MacPackageHasRelativeSymbolicLinks(installerFile);
        }

        private void MacPackageHasRelativeSymbolicLinks(string installerFile) =>
            SymbolicLinkHelpers.VerifyPackageSymlinks(installerFile, "pkg", tempDir =>
            {
                // Extract .pkg file using pkgutil to a subdirectory
                var expandDir = Path.Combine(tempDir, "expanded");
                Directory.CreateDirectory(expandDir);

                Log.WriteLine($"Expanding package: {installerFile}");
                new RunExeCommand(Log, "pkgutil")
                    .Execute("--expand", installerFile, expandDir)
                    .Should().Pass();

                // Find the Payload file(s) - typically cpio archives
                var payloadFiles = Directory.GetFiles(expandDir, "Payload", SearchOption.AllDirectories);

                if (payloadFiles.Length == 0)
                {
                    Assert.Fail("No Payload file found in the expanded package");
                }

                // Extract each Payload using cpio directly to tempDir
                foreach (var payloadFile in payloadFiles)
                {
                    Log.WriteLine($"Extracting payload: {payloadFile}");
                    new RunExeCommand(Log, "sh")
                        .WithWorkingDirectory(tempDir)
                        .Execute("-c", $"cat '{payloadFile}' | gunzip -dc | cpio -i 2>&1")
                        .Should().Pass();
                }
            }, Log);
    }
}
