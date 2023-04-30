// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Diagnostics Ids for package validation errors.
    /// </summary>
    public static class DiagnosticIds
    {
        // Assembly loading ids
        public const string SearchDirectoriesNotFoundForTfm = "CP1003";

        public const string ApplicableCompileTimeAsset = "PKV0001";
        public const string ApplicableRuntimeSpecificAsset = "PKV0002";
        public const string ApplicableRidLessAsset = "PKV003";
        public const string CompatibleRuntimeRidLessAsset = "PKV004";
        public const string CompatibleRuntimeRidSpecificAsset = "PKV005";
        public const string TargetFrameworkDropped = "PKV006";
        public const string TargetFrameworkAndRidPairDropped = "PKV007";
    }
}
