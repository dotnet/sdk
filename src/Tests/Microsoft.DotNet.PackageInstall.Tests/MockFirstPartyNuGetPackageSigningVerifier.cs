﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class MockFirstPartyNuGetPackageSigningVerifier: IFirstPartyNuGetPackageSigningVerifier
    {
        private readonly bool _isExecutableIsFirstPartySignedWithoutValidation;
        private readonly bool _verifyResult;
        private readonly string _commandOutput;

        
        public MockFirstPartyNuGetPackageSigningVerifier(bool isExecutableIsFirstPartySignedWithoutValidation = true, bool verifyResult = true, string commandOutput = "")
        {
            _isExecutableIsFirstPartySignedWithoutValidation = isExecutableIsFirstPartySignedWithoutValidation;
            _verifyResult = verifyResult;
            _commandOutput = commandOutput;
        }

        public bool Verify(FilePath nupkgToVerify, out string commandOutput)
        {
            commandOutput = _commandOutput;
            return _verifyResult;
        }

        public bool IsExecutableIsFirstPartySignedWithoutValidation(FilePath executable)
        {
            return _isExecutableIsFirstPartySignedWithoutValidation;
        }
    }
}
