// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet.Cli.Installer.Windows;
using Microsoft.DotNet.Cli.Installer.Windows.Security;

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

        [WindowsOnlyFact]
        public void InstallRequestMessageCreateThrowsForNullPayload()
        {
            Action action = () => InstallRequestMessage.Create(System.Text.Encoding.UTF8.GetBytes("null"));

            action.Should().Throw<System.Text.Json.JsonException>();
        }

        [WindowsOnlyFact]
        public void InstallResponseMessageCreateThrowsForNullPayload()
        {
            Action action = () => InstallResponseMessage.Create(System.Text.Encoding.UTF8.GetBytes("null"));

            action.Should().Throw<System.Text.Json.JsonException>();
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

        [WindowsOnlyFact]
        public void CreatePipeSecurity_ShouldNotGrantAccessToAuthenticatedUsers()
        {
            SecurityIdentifier ownerSid = WindowsIdentity.GetCurrent().Owner;
            SecurityIdentifier clientSid = WindowsUtils.GetPipeClientIdentifier();

            PipeSecurity pipeSecurity = WindowsUtils.CreatePipeSecurity(ownerSid, clientSid);

            var rules = pipeSecurity.GetAccessRules(true, false, typeof(SecurityIdentifier));
            SecurityIdentifier authenticatedUserSid = new(WellKnownSidType.AuthenticatedUserSid, null);

            Assert.DoesNotContain(rules.Cast<PipeAccessRule>(),
                r => r.IdentityReference.Equals(authenticatedUserSid) && r.AccessControlType == AccessControlType.Allow);
        }

        [WindowsOnlyFact]
        public void ValidateLogFilePath_ShouldRejectSystemPaths()
        {
            string maliciousPath = @"C:\Windows\System32\evil.log";

            string result = WindowsUtils.ValidateLogFilePath(maliciousPath);
            Assert.NotEqual(maliciousPath, result);
        }

        [WindowsOnlyFact]
        public void ValidateLogFilePath_ShouldAcceptUserProfileTempPath()
        {
            // Use a fake server temp that differs from the user's profile temp,
            // forcing the validation to exercise the profile-based lookup path.
            string fakeServerTemp = @"C:\Windows\Temp";
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string userTempPath = Path.Combine(userProfile, "AppData", "Local", "Temp", "Microsoft.NET.Workload_test.log");

            string result = WindowsUtils.ValidateLogFilePath(userTempPath, fakeServerTemp);
            Assert.Equal(Path.GetFullPath(userTempPath), result);
        }

        [WindowsOnlyFact]
        public void ValidateLogFilePath_ShouldRejectTraversalAttack()
        {
            string traversalPath = Path.Combine(Path.GetTempPath(), @"..\..\Windows\System32\evil.log");

            string result = WindowsUtils.ValidateLogFilePath(traversalPath);
            string canonicalized = Path.GetFullPath(traversalPath);

            // The traversal resolves to a system path, so it should be redirected
            Assert.NotEqual(canonicalized, result);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), result, StringComparison.OrdinalIgnoreCase);
        }

        [WindowsOnlyFact]
        public void ValidatePackagePath_ShouldRejectTraversalAttack()
        {
            string cacheRoot = @"C:\ProgramData\dotnet\workloads";
            string traversalPath = cacheRoot + @"\..\..\..\..\Users\Public\evil.msi";

            Assert.False(WindowsUtils.ValidatePackagePath(traversalPath, cacheRoot));
        }

        [WindowsOnlyFact]
        public void ValidatePackagePath_ShouldRejectSiblingPrefixAttack()
        {
            string cacheRoot = @"C:\ProgramData\dotnet\workloads";
            string siblingPath = @"C:\ProgramData\dotnet\workloadsEvil\evil.msi";

            Assert.False(WindowsUtils.ValidatePackagePath(siblingPath, cacheRoot));
        }

        [WindowsOnlyFact]
        public void ValidatePackagePath_ShouldAcceptValidCachePath()
        {
            string cacheRoot = @"C:\ProgramData\dotnet\workloads";
            string validPath = @"C:\ProgramData\dotnet\workloads\pack\1.0\pack.msi";

            Assert.True(WindowsUtils.ValidatePackagePath(validPath, cacheRoot));
        }

        [WindowsOnlyFact]
        public void ValidatePathComponent_ShouldRejectTraversalSequence()
        {
            Assert.False(WindowsUtils.ValidatePathComponent(@"..\..\evil"));
        }

        [WindowsOnlyFact]
        public void ValidatePathComponent_ShouldRejectDirectorySeparator()
        {
            Assert.False(WindowsUtils.ValidatePathComponent(@"good\evil"));
        }

        [WindowsOnlyFact]
        public void ValidatePathComponent_ShouldRejectAltDirectorySeparator()
        {
            Assert.False(WindowsUtils.ValidatePathComponent("good/evil"));
        }

        [WindowsOnlyFact]
        public void ValidatePathComponent_ShouldRejectEmpty()
        {
            Assert.False(WindowsUtils.ValidatePathComponent(""));
            Assert.False(WindowsUtils.ValidatePathComponent(null));
        }

        [WindowsOnlyFact]
        public void ValidatePathComponent_ShouldAcceptValidComponent()
        {
            Assert.True(WindowsUtils.ValidatePathComponent("Microsoft.NET.Workload.Mono.ToolChain"));
            Assert.True(WindowsUtils.ValidatePathComponent("8.0.100"));
        }

        [WindowsOnlyFact]
        public void ValidatePathUnderRoot_ShouldRejectTraversal()
        {
            string root = @"C:\ProgramData\dotnet\workloads";
            string traversal = root + @"\..\..\..\..\Windows\System32\evil.msi";

            Assert.False(WindowsUtils.ValidatePathUnderRoot(traversal, root));
        }

        [WindowsOnlyFact]
        public void ValidatePathUnderRoot_ShouldRejectSiblingPrefix()
        {
            string root = @"C:\ProgramData\dotnet\workloads";
            string sibling = @"C:\ProgramData\dotnet\workloadsEvil\evil.msi";

            Assert.False(WindowsUtils.ValidatePathUnderRoot(sibling, root));
        }

        [WindowsOnlyFact]
        public void ValidatePathUnderRoot_ShouldAcceptValidPath()
        {
            string root = @"C:\ProgramData\dotnet\workloads";
            string valid = @"C:\ProgramData\dotnet\workloads\pack\1.0\manifest.json";

            Assert.True(WindowsUtils.ValidatePathUnderRoot(valid, root));
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
