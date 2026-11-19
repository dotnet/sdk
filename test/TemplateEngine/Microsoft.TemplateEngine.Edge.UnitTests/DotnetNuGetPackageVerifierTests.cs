// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Security;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.TestHelper;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class DotnetNuGetPackageVerifierTests : SdkTest
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;
        private static PackageManager s_packageManager = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            s_environmentSettingsHelper = new EnvironmentSettingsHelper();
            s_packageManager = new PackageManager();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            s_packageManager?.Dispose();
            s_environmentSettingsHelper?.Dispose();
        }

        [TestMethod]
        public async Task VerifyAsync_ValidSignature_Succeeds()
        {
            string packagePath = await s_packageManager.GetNuGetPackage("Newtonsoft.Json", "13.0.3");

            await VerifyAsync(packagePath);
        }

        [TestMethod]
        public async Task VerifyAsync_InvalidSignature_IsRejected()
        {
            string signedPackage = await s_packageManager.GetNuGetPackage("Newtonsoft.Json", "13.0.3");
            string packagePath = Path.Combine(s_environmentSettingsHelper.CreateTemporaryFolder(), "Tampered Package.nupkg");
            File.Copy(signedPackage, packagePath);
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update))
            {
                using var writer = new StreamWriter(archive.CreateEntry("tampered.txt").Open());
                writer.Write("Content added after signing.");
            }

            var exception = await Assert.ThrowsExactlyAsync<SecurityException>(() => VerifyAsync(packagePath));

            exception.Message.Should().Contain("dotnet nuget verify --all");
            exception.Message.Should().Contain("failed with exit code 1");
        }

        [TestMethod]
        public async Task VerifyAsync_MissingSignature_AcceptPolicy_Succeeds()
        {
            string packagePath = CreateUnsignedPackage();

            await VerifyAsync(packagePath, "accept");
        }

        [TestMethod]
        public async Task VerifyAsync_MissingSignature_RequirePolicy_IsRejected()
        {
            string packagePath = CreateUnsignedPackage();

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => VerifyAsync(packagePath, "require"));
        }

        private string CreateUnsignedPackage()
        {
            string packagePath = Path.Combine(s_environmentSettingsHelper.CreateTemporaryFolder(), "Unsigned Package.nupkg");
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("TestPackage.nuspec").Open());
                writer.Write("<package><metadata><id>TestPackage</id><version>1.0.0</version><authors>Test</authors><description>Test package</description></metadata></package>");
            }

            return packagePath;
        }

        private Task VerifyAsync(string packagePath, string policy = "accept")
        {
            string directory = s_environmentSettingsHelper.CreateTemporaryFolder();
            File.WriteAllText(
                Path.Combine(directory, "NuGet.config"),
                $"""
                <configuration>
                    <config>
                        <add key="signatureValidationMode" value="{policy}" />
                    </config>
                    <trustedSigners>
                        <clear />
                    </trustedSigners>
                </configuration>
                """);
            ISettings settings = global::NuGet.Configuration.Settings.LoadSpecificSettings(directory, "NuGet.config");
            return new DotnetNuGetPackageVerifier().VerifyAsync(
                packagePath,
                Repository.Factory.GetCoreV3(directory),
                settings,
                NullLogger.Instance,
                TestContext.CancellationToken);
        }
    }
}
