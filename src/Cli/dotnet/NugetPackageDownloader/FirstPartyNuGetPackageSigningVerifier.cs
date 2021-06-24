﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using HashAlgorithmName = System.Security.Cryptography.HashAlgorithmName;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class FirstPartyNuGetPackageSigningVerifier : IFirstPartyNuGetPackageSigningVerifier
    {
        private readonly HashSet<string> _firstPartyCertificateThumbprints =
            new() {"3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE"};

        private readonly HashSet<string> _upperFirstPartyCertificateThumbprints =
            new() {"51044706BD237B91B89B781337E6D62656C69F0FCFFBE8E43741367948127862"};

        private const string FirstPartyCertificateSubject =
            "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";

        private DirectoryPath _tempDirectory;
        private ILogger _logger;

        public FirstPartyNuGetPackageSigningVerifier(DirectoryPath? tempDirectory = null, ILogger logger = null)
        {
            _tempDirectory = tempDirectory ?? new DirectoryPath(Path.GetTempPath());
            _logger = logger ?? new NullLogger();
        }

        public bool Verify(FilePath nupkgToVerify, out string commandOutput)
        {
            return NuGetVerify(nupkgToVerify, out commandOutput) && IsFirstParty(nupkgToVerify);
        }

        internal bool IsFirstParty(FilePath nupkgToVerify)
        {
            var packageReader = new PackageArchiveReader(nupkgToVerify.Value);
            Directory.CreateDirectory(_tempDirectory.Value);
            FilePath targetFilePath = _tempDirectory.WithFile(Path.GetRandomFileName());
            try
            {
                packageReader.ExtractFile(".signature.p7s", targetFilePath.Value, _logger);
                using var fs = new FileStream(targetFilePath.Value, FileMode.Open);
                PrimarySignature primarySignature = PrimarySignature.Load(fs);
                IX509CertificateChain certificateChain = SignatureUtility.GetCertificateChain(primarySignature);

                if (certificateChain.Count < 2)
                {
                    return false;
                }

                X509Certificate2 firstCert = certificateChain.First();
                if (_firstPartyCertificateThumbprints.Contains(firstCert.GetCertHashString(HashAlgorithmName.SHA256),
                    StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (firstCert.Subject.Equals(FirstPartyCertificateSubject, StringComparison.OrdinalIgnoreCase)
                    && _upperFirstPartyCertificateThumbprints.Contains(
                        certificateChain[1].GetCertHashString(HashAlgorithmName.SHA256),
                        StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        private static bool NuGetVerify(FilePath nupkgToVerify, out string commandOutput)
        {
            var args = new[] {"verify", "--all", nupkgToVerify.Value};
            var command = new DotNetCommandFactory(alwaysRunOutOfProc: true)
                .Create("nuget", args);

            var commandResult = command.CaptureStdOut().Execute();
            commandOutput = commandResult.StdOut + Environment.NewLine + commandResult.StdErr;
            return commandResult.ExitCode == 0;
        }
        
        public bool IsExecutableIsFirstPartySignedWithoutValidation(FilePath executable)
        {
            try
            {
                X509Certificate signedFile = X509Certificate2.CreateFromSignedFile(executable.Value);
                return signedFile.Subject.Contains("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
