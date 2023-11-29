// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
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
        // This verifies E_TRUST_BAD_DIGEST
        [InlineData("tampered.msi", -2146869232, "The digital signature of the object did not verify.")]
        [InlineData("dual_signed.dll", 0, "")]
        [InlineData("dotnet_realsigned.exe", 0, "")]
        // This verifies CERT_E_UNTRUSTEDROOT
        [InlineData("dotnet_fakesigned.exe", -2146762487, "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.")]
        public void AuthentiCodeSignaturesCanBeVerified(string file, int expectedStatus, string expectedError)
        {
            int status = Signature.IsAuthenticodeSigned(Path.Combine(s_testDataPath, file));
            Assert.Equal(expectedStatus, status);

            if (expectedStatus != 0)
            {
                Assert.Equal(expectedError, Marshal.GetPInvokeErrorMessage(status));
            }
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
