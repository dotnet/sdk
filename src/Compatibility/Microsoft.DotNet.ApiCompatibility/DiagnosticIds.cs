﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class containing the strings representing the Diagnostic IDs that can be returned in the compatibility differences.
    /// </summary>
    public static class DiagnosticIds
    {
        public const string TypeMustExist = "CP0001";
        public const string MemberMustExist = "CP0002";
        public const string AssemblyIdentityMustMatch = "CP0003";
        public const string MatchingAssemblyDoesNotExist = "CP0004";
        public const string CannotAddAbstractMember = "CP0005";
        public const string CannotAddMemberToInterface = "CP0006";
    }
}
