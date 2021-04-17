// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Interface to implement different kinds of validations for a package.
    /// </summary>
    public interface IPackageValidator
    {
        DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> Validate(Package package);
    }
}
