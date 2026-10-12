// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>Uses the Microsoft-signed dotnet host as a signed executable and the fixture apphost as an unsigned one.</summary>
[TestClass]
public class SelfUpdateSignatureTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void SignedInstallationRejectsUnsignedReplacement()
    {
        using var files = new SelfUpdateTestFiles();
        var installed = CopySignedHost(files, "installed.exe");

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            SelfUpdateSignature.VerifyReplacement(installed, files.Paths.StagedPath));

        Assert.AreEqual(DotnetInstallErrorCode.SignatureVerificationFailed, exception.ErrorCode);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void SignedInstallationAcceptsMicrosoftSignedReplacement()
    {
        using var files = new SelfUpdateTestFiles();
        var installed = CopySignedHost(files, "installed.exe");
        var staged = CopySignedHost(files, "staged.exe");

        SelfUpdateSignature.VerifyReplacement(installed, staged);
    }

    [TestMethod]
    public void UnsignedInstallationAcceptsUnsignedReplacement()
    {
        using var files = new SelfUpdateTestFiles();

        SelfUpdateSignature.VerifyReplacement(files.Paths.InstalledPath, files.Paths.StagedPath);
    }

    [SupportedOSPlatform("windows")]
    private static string CopySignedHost(SelfUpdateTestFiles files, string name)
    {
        var host = SelfUpdateTestFiles.DotnetHostPath;
        if (!IsAuthenticodeSigned(host))
        {
            Assert.Inconclusive($"The dotnet host '{host}' is not Authenticode-signed.");
        }

        var path = Path.Combine(files.Paths.DirectoryPath, name);
        File.Copy(host, path);
        return path;
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAuthenticodeSigned(string path)
    {
#pragma warning disable SYSLIB0057 // Authenticode signer extraction is not available from X509CertificateLoader.
        try
        {
            using var signer = X509Certificate.CreateFromSignedFile(path);
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
#pragma warning restore SYSLIB0057
    }
}
