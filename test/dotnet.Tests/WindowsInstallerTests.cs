// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Installer.Windows;
using Microsoft.DotNet.Cli.Installer.Windows.Security;

namespace Microsoft.DotNet.Tests
{
    [SupportedOSPlatform("windows5.1.2600")]
    [TestClass]
    public class WindowsInstallerTests
    {
        private static string s_testDataPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestData");
        public TestContext TestContext { get; set; } = null!;

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

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void MultipleProcessesCanWriteToTheLog()
        {
            var logFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            TimestampedFileLogger logger = new(logFile);

            logger.AddNamedPipe("np1");
            logger.AddNamedPipe("np2");
            logger.AddNamedPipe("np3");

            var t1 = Task.Run(() => { LogTask("np1"); }, TestContext.CancellationToken);
            var t2 = Task.Run(() => { LogTask("np2"); }, TestContext.CancellationToken);
            var t3 = Task.Run(() => { LogTask("np3"); }, TestContext.CancellationToken);

            Task.WaitAll(t1, t2, t3);
            logger.Dispose();

            string logContent = File.ReadAllText(logFile);

            Assert.Contains("Hello from np1", logContent);
            Assert.Contains("Hello from np2", logContent);
            Assert.Contains("Hello from np3", logContent);
            Assert.Contains("=== Logging ended ===", logContent);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
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
            }, TestContext.CancellationToken);

            cd.Connect();

            InstallResponseMessage r1 = cd.SendMsiRequest(InstallRequestType.UninstallMsi, "");
            InstallResponseMessage r2 = cd.SendShutdownRequest();

            Assert.AreEqual("Received request: UninstallMsi", r1.Message);
            Assert.AreEqual("Shutting down!", r2.Message);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void InstallRequestMessageCreateThrowsForNullPayload()
        {
            Action action = () => InstallRequestMessage.Create(System.Text.Encoding.UTF8.GetBytes("null"));

            action.Should().Throw<System.Text.Json.JsonException>();
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void InstallResponseMessageCreateThrowsForNullPayload()
        {
            Action action = () => InstallResponseMessage.Create(System.Text.Encoding.UTF8.GetBytes("null"));

            action.Should().Throw<System.Text.Json.JsonException>();
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [DataRow("1033,1041,1049", UpgradeAttributes.MigrateFeatures, 1041, false)]
        [DataRow(null, UpgradeAttributes.LanguagesExclusive, 3082, false)]
        [DataRow("1033,1041,1049", UpgradeAttributes.LanguagesExclusive, 1033, true)]
        public void RelatedProductExcludesLanguages(string language, UpgradeAttributes attributes, int lcid,
            bool expectedResult)
        {
            RelatedProduct rp = new()
            {
                Attributes = attributes,
                Language = language
            };

            Assert.AreEqual(expectedResult, rp.ExcludesLanguage(lcid));
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [DataRow("72.13.638", UpgradeAttributes.MigrateFeatures, "72.13.639", true)]
        [DataRow("72.13.638", UpgradeAttributes.VersionMaxInclusive, "72.13.638", false)]
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

            Assert.AreEqual(expectedResult, rp.ExcludesMaxVersion(installedVersion));
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [DataRow("72.13.638", UpgradeAttributes.MigrateFeatures, "72.13.638", true)]
        [DataRow("72.13.638", UpgradeAttributes.VersionMinInclusive, "72.13.638", false)]
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

            Assert.AreEqual(expectedResult, rp.ExcludesMinVersion(installedVersion));
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        // This verifies E_TRUST_BAD_DIGEST (file was modified after being signed)
        [DataRow(@"tampered.msi", -2146869232)]
        [DataRow(@"dual_signed.dll", 0)]
        [DataRow(@"dotnet_realsigned.exe", 0)]
        // Signed by the .NET Foundation, terminates in a DigiCert root, so should be accepted by the Authenticode trust provider.
        [DataRow(@"BootstrapperCore.dll", 0)]
        // Old SHA1 certificate, but still a valid signature.
        [DataRow(@"system.web.mvc.dll", 0)]
        public void AuthentiCodeSignaturesCanBeVerified(string file, int expectedStatus)
        {
            int status = Signature.IsAuthenticodeSigned(Path.Combine(s_testDataPath, file));
            Assert.AreEqual(expectedStatus, status);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        [DataRow(@"dotnet_realsigned.exe", 0)]
        // Valid SHA1 signature, but no longer considered a trusted root certificate, should return CERT_E_UNTRUSTEDROOT.
        [DataRow(@"system.web.mvc.dll", -2146762487)]
        // The first certificate chain terminates in a non-Microsoft root so it fails the policy. Workloads do not currently support
        // 3rd party installers. If we change that policy and we sign installers with the Microsoft 3rd Party certificate we will need to extract the nested
        // signature and verify that at least one chain terminates in a Microsoft root. The WinTrust logic will also need to be updated to verify each
        // chain.
        [DataRow(@"dual_signed.dll", -2146762487)]
        // DigiCert root should fail the policy check because it's not a trusted Microsoft root certificate.
        [DataRow(@"BootstrapperCore.dll", -2146762487)]
        // Digest will fail verification, BUT the root certificate in the chain is a trusted root.
        [DataRow(@"tampered.msi", 0)]
        public void ItVerifiesTrustedMicrosoftRootCertificateChainPolicy(string file, int expectedResult)
        {
            int result = Signature.HasMicrosoftTrustedRoot(Path.Combine(s_testDataPath, file));

            Assert.AreEqual(expectedResult, result);
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
