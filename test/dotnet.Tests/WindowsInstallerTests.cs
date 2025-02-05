// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.Installer.Windows.Security;

namespace Microsoft.DotNet.Tests
{
    [SupportedOSPlatform("windows5.1.2600")]
    public class WindowsInstallerTests
    {
        private static string s_testDataPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestData");

        private void LogTask(string pipeName)
        {
            using NamedPipeServerStream serverPipe = CreateServerPipe(pipeName);
            PipeStreamSetupLogger logger = new(serverPipe, pipeName);

            logger.Connect();

            for (int i = 0; i < 10; i++)
            {
                logger.LogMessage($"Hello from {pipeName} ({i}).");
            }
        }

        [WindowsOnlyFact]
        public void MultipleProcessesCanWriteToTheLog()
        {
            var logFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            TimestampedFileLogger logger = new(logFile);

            logger.AddNamedPipe("np1");
            logger.AddNamedPipe("np2");
            logger.AddNamedPipe("np3");

            var t1 = Task.Run(() => { LogTask("np1"); });
            var t2 = Task.Run(() => { LogTask("np2"); });
            var t3 = Task.Run(() => { LogTask("np3"); });

            Task.WaitAll(t1, t2, t3);
            logger.Dispose();

            string logContent = File.ReadAllText(logFile);

            Assert.Contains("Hello from np1", logContent);
            Assert.Contains("Hello from np2", logContent);
            Assert.Contains("Hello from np3", logContent);
            Assert.Contains("=== Logging ended ===", logContent);
        }

        [WindowsOnlyFact]
        public void InstallMessageDispatcherProcessesMessages()
        {
            string pipeName = Guid.NewGuid().ToString();
            NamedPipeServerStream serverPipe = CreateServerPipe(pipeName);
            NamedPipeClientStream clientPipe = new(".", pipeName, PipeDirection.InOut);

            InstallMessageDispatcher sd = new(serverPipe);
            InstallMessageDispatcher cd = new(clientPipe);

            Task.Run(() =>
            {
                ServerDispatcher server = new(sd);
                server.Run();
            });

            cd.Connect();

            InstallResponseMessage r1 = cd.SendMsiRequest(InstallRequestType.UninstallMsi, "");
            InstallResponseMessage r2 = cd.SendShutdownRequest();

            Assert.Equal("Received request: UninstallMsi", r1.Message);
            Assert.Equal("Shutting down!", r2.Message);
        }

        [WindowsOnlyTheory]
        [InlineData("1033,1041,1049", UpgradeAttributes.MigrateFeatures, 1041, false)]
        [InlineData(null, UpgradeAttributes.LanguagesExclusive, 3082, false)]
        [InlineData("1033,1041,1049", UpgradeAttributes.LanguagesExclusive, 1033, true)]
        public void RelatedProductExcludesLanguages(string language, UpgradeAttributes attributes, int lcid,
            bool expectedResult)
        {
            RelatedProduct rp = new()
            {
                Attributes = attributes,
                Language = language
            };

            Assert.Equal(expectedResult, rp.ExcludesLanguage(lcid));
        }

        [WindowsOnlyTheory]
        [InlineData("72.13.638", UpgradeAttributes.MigrateFeatures, "72.13.639", true)]
        [InlineData("72.13.638", UpgradeAttributes.VersionMaxInclusive, "72.13.638", false)]
        public void RelatedProductExcludesMaxVersion(string maxVersion, UpgradeAttributes attributes, string installedVersionValue,
            bool expectedResult)
        {
            Version installedVersion = new(installedVersionValue);

            RelatedProduct rp = new()
            {
                Attributes = attributes,
                VersionMax = maxVersion == null ? null : new Version(maxVersion),
                VersionMin = null
            };

            Assert.Equal(expectedResult, rp.ExcludesMaxVersion(installedVersion));
        }

        [WindowsOnlyTheory]
        [InlineData("72.13.638", UpgradeAttributes.MigrateFeatures, "72.13.638", true)]
        [InlineData("72.13.638", UpgradeAttributes.VersionMinInclusive, "72.13.638", false)]
        public void RelatedProductExcludesMinVersion(string minVersion, UpgradeAttributes attributes, string installedVersionValue,
            bool expectedResult)
        {
            Version installedVersion = new(installedVersionValue);

            RelatedProduct rp = new()
            {
                Attributes = attributes,
                VersionMin = minVersion == null ? null : new Version(minVersion),
                VersionMax = null
            };

            Assert.Equal(expectedResult, rp.ExcludesMinVersion(installedVersion));
        }

        [WindowsOnlyTheory]
        // This verifies E_TRUST_BAD_DIGEST (file was modified after being signed)
        [InlineData(@"tampered.msi", -2146869232)]
        [InlineData(@"dual_signed.dll", 0)]
        [InlineData(@"dotnet_realsigned.exe", 0)]
        // Signed by the .NET Foundation, terminates in a DigiCert root, so should be accepted by the Authenticode trust provider.
        [InlineData(@"BootstrapperCore.dll", 0)]
        // Old SHA1 certificate, but still a valid signature.
        [InlineData(@"system.web.mvc.dll", 0)]
        public void AuthentiCodeSignaturesCanBeVerified(string file, int expectedStatus)
        {
            int status = Signature.IsAuthenticodeSigned(Path.Combine(s_testDataPath, file));
            Assert.Equal(expectedStatus, status);
        }

        [WindowsOnlyTheory]
        [InlineData(@"dotnet_realsigned.exe", 0)]
        // Valid SHA1 signature, but no longer considered a trusted root certificate, should return CERT_E_UNTRUSTEDROOT.
        [InlineData(@"system.web.mvc.dll", -2146762487)]
        // The first certificate chain terminates in a non-Microsoft root so it fails the policy. Workloads do not currently support
        // 3rd party installers. If we change that policy and we sign installers with the Microsoft 3rd Party certificate we will need to extract the nested
        // signature and verify that at least one chain terminates in a Microsoft root. The WinTrust logic will also need to be updated to verify each
        // chain.
        [InlineData(@"dual_signed.dll", -2146762487)]
        // DigiCert root should fail the policy check because it's not a trusted Microsoft root certificate.
        [InlineData(@"BootstrapperCore.dll", -2146762487)]
        // Digest will fail verification, BUT the root certificate in the chain is a trusted root.
        [InlineData(@"tampered.msi", 0)]
        public void ItVerifiesTrustedMicrosoftRootCertificateChainPolicy(string file, int expectedResult)
        {
            int result = Signature.HasMicrosoftTrustedRoot(Path.Combine(s_testDataPath, file));

            Assert.Equal(expectedResult, result);
        }

        private NamedPipeServerStream CreateServerPipe(string name)
        {
            return new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Message);
        }
    }

    [SupportedOSPlatform("windows")]
    internal class ServerDispatcher
    {
        InstallMessageDispatcher _dispatcher;

        public ServerDispatcher(InstallMessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Run()
        {
            _dispatcher.Connect();
            bool done = false;

            while (!done)
            {
                if (_dispatcher == null || !_dispatcher.IsConnected)
                {
                    throw new IOException("Server dispatcher disconnected or not initialized.");
                }

                var request = _dispatcher.ReceiveRequest();

                if (request.RequestType == InstallRequestType.Shutdown)
                {
                    done = true;
                    _dispatcher.ReplySuccess("Shutting down!");
                }
                else
                {
                    _dispatcher.ReplySuccess($"Received request: {request.RequestType}");
                }
            }
        }
    }
}
