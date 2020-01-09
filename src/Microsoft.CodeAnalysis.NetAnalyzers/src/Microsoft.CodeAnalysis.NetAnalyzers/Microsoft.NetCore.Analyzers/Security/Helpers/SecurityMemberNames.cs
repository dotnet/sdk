// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    internal static class SecurityMemberNames
    {
        // This is nameof(System.Security.Cryptography.DSA.CreateSignature), but DSA doesn't exist in .NET Standard 1.3.
        public const string CreateSignature = nameof(CreateSignature);
    }
}
