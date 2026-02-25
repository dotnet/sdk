// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    internal static class SecurityMemberNames
    {
        // This is nameof(System.Security.Cryptography.DSA.CreateSignature), but DSA doesn't exist in .NET Standard 1.3.
        public const string CreateSignature = nameof(CreateSignature);
    }
}
