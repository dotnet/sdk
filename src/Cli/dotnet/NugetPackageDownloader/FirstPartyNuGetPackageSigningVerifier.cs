// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using HashAlgorithmName = System.Security.Cryptography.HashAlgorithmName;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader;

/// <summary>
/// Verifies NuGet package (<c>.nupkg</c>) signatures.
/// </summary>
/// <remarks>
/// <para>Provides two levels of verification:</para>
/// <list type="bullet">
///   <item><see cref="Verify"/> — <b>Microsoft first-party check</b>: calls <see cref="NuGetVerify"/>
///     to validate the signature chain, then calls <see cref="IsFirstParty"/> to verify the
///     <b>author</b> signing certificate matches known Microsoft thumbprints. Used when package
///     source mapping is not in use (the default workload path). Workloads are selected from a
///     Microsoft-provided list, so there is an implicit chain of trust justifying this check.</item>
///   <item><see cref="NuGetVerify"/> — <b>Any valid NuGet signature</b>: shells out to
///     <c>dotnet nuget verify --all</c> to confirm the package has a valid signature from any trusted
///     signer (repository or author). Used when package source mapping is enabled, since feed
///     constraints already limit which packages are accepted.</item>
/// </list>
/// <para>This is distinct from MSI Authenticode verification, which is handled by
/// <see cref="Installer.Windows.MsiPackageCache"/> for Windows MSI payloads.</para>
/// </remarks>
internal class FirstPartyNuGetPackageSigningVerifier : IFirstPartyNuGetPackageSigningVerifier
{
    /// <summary>
    /// SHA-256 thumbprints of known Microsoft first-party signing certificates (leaf certificates).
    /// If the package's primary signature leaf certificate matches one of these, it is considered first-party.
    /// </summary>
    internal readonly HashSet<string> _firstPartyCertificateThumbprints =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE",
            "AA12DA22A49BCE7D5C1AE64CC1F3D892F150DA76140F210ABD2CBFFCA2C18A27",
            "566A31882BE208BE4422F7CFD66ED09F5D4524A5994F50CCC8B05EC0528C1353"
        };

    /// <summary>
    /// SHA-256 thumbprints of intermediate certificates in the signing chain. Packages are considered
    /// first-party when the leaf certificate subject matches <see cref="FirstPartyCertificateSubject"/>
    /// AND the second certificate in the chain matches one of these thumbprints.
    /// </summary>
    private readonly HashSet<string> _upperFirstPartyCertificateThumbprints =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "51044706BD237B91B89B781337E6D62656C69F0FCFFBE8E43741367948127862",
            "46011EDE1C147EB2BC731A539B7C047B7EE93E48B9D3C3BA710CE132BBDFAC6B"
        };

    private const string FirstPartyCertificateSubject =
        "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

    public FirstPartyNuGetPackageSigningVerifier()
    {
    }

    /// <summary>
    /// Verifies that the package has a valid NuGet signature AND is signed by a known Microsoft first-party certificate.
    /// </summary>
    /// <remarks>
    /// This is the <b>strict</b> verification mode. It first calls <see cref="NuGetVerify"/> to confirm
    /// the package has a valid signature, then calls <see cref="IsFirstParty"/> to check the signing
    /// certificate against known Microsoft thumbprints.
    /// </remarks>
    /// <param name="nupkgToVerify">Path to the <c>.nupkg</c> file.</param>
    /// <param name="commandOutput">Diagnostic output from the NuGet verify command.</param>
    /// <returns><see langword="true"/> if the package is validly signed by Microsoft.</returns>
    public bool Verify(FilePath nupkgToVerify, out string commandOutput)
    {
        return NuGetVerify(nupkgToVerify, out commandOutput) && IsFirstParty(nupkgToVerify);
    }

    /// <summary>
    /// Checks whether the NuGet package's primary signature was produced by a known Microsoft first-party certificate.
    /// </summary>
    /// <remarks>
    /// Two matching strategies are used:
    /// <list type="number">
    ///   <item>Leaf certificate SHA-256 thumbprint matches <see cref="_firstPartyCertificateThumbprints"/>.</item>
    ///   <item>Leaf certificate subject matches <see cref="FirstPartyCertificateSubject"/> AND the
    ///         intermediate (second) certificate thumbprint matches <see cref="_upperFirstPartyCertificateThumbprints"/>.</item>
    /// </list>
    /// This does NOT validate the signature itself — only the identity of the signer.
    /// Call <see cref="NuGetVerify"/> first for signature validation.
    /// </remarks>
    internal bool IsFirstParty(FilePath nupkgToVerify)
    {
        try
        {
            using (var packageReader = new PackageArchiveReader(nupkgToVerify.Value))
            {
                PrimarySignature primarySignature = packageReader.GetPrimarySignatureAsync(CancellationToken.None).GetAwaiter().GetResult();
                using (IX509CertificateChain certificateChain = SignatureUtility.GetCertificateChain(primarySignature))
                {
                    if (certificateChain.Count < 2)
                    {
                        return false;
                    }

                    X509Certificate2 firstCert = certificateChain.First();
                    if (_firstPartyCertificateThumbprints.Contains(firstCert.GetCertHashString(HashAlgorithmName.SHA256)))
                    {
                        return true;
                    }

                    if (firstCert.Subject.Equals(FirstPartyCertificateSubject, StringComparison.OrdinalIgnoreCase)
                        && _upperFirstPartyCertificateThumbprints.Contains(
                            certificateChain[1].GetCertHashString(HashAlgorithmName.SHA256)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies that the NuGet package has any valid signature by running <c>dotnet nuget verify --all</c>.
    /// </summary>
    /// <remarks>
    /// This is the <b>relaxed</b> verification mode. It does NOT check whether the signer is Microsoft —
    /// any trusted signer is accepted. Used when package source mapping is enabled, since the feed
    /// constraints already limit which packages are accepted.
    /// <para>On Linux, the subprocess finds the TRP root certificate bundles shipped with the SDK
    /// automatically. On macOS, verification may fail unless the bundles are present.</para>
    /// </remarks>
    /// <param name="nupkgToVerify">Path to the <c>.nupkg</c> file.</param>
    /// <param name="commandOutput">Combined stdout + stderr from the verify command.</param>
    /// <param name="currentWorkingDirectory">Working directory for NuGet config resolution (optional).</param>
    /// <returns><see langword="true"/> if the package signature is valid.</returns>
    public static bool NuGetVerify(FilePath nupkgToVerify, out string commandOutput, string currentWorkingDirectory = null)
    {
        var args = new[] { "verify", "--all", nupkgToVerify.Value };
        var command = new DotNetCommandFactory(alwaysRunOutOfProc: true, currentWorkingDirectory)
            .Create("nuget", args);

        var commandResult = command.CaptureStdOut().Execute();
        commandOutput = commandResult.StdOut + Environment.NewLine + commandResult.StdErr;
        return commandResult.ExitCode == 0;
    }
}
