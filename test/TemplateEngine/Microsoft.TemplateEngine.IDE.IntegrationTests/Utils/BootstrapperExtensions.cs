// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests.Utils
{
    internal static class BootstrapperExtensions
    {
        internal static async Task InstallTestTemplateAsync(this Bootstrapper bootstrapper, params string[] templates)
        {
            List<InstallRequest> installRequests = new List<InstallRequest>();

            foreach (string template in templates)
            {
                string path = TestUtils.GetTestTemplateLocation(template);
                installRequests.Add(new InstallRequest(Path.GetFullPath(path)));
            }

            IReadOnlyList<InstallResult> installationResults = await bootstrapper.InstallTemplatePackagesAsync(installRequests).ConfigureAwait(false);
            if (installationResults.Any(result => !result.Success))
            {
                throw new Exception($"Failed to install templates: {string.Join(";", installationResults.Select(result => $"path: {result.InstallRequest.PackageIdentifier}, details:{result.ErrorMessage}"))}");
            }
        }

        internal static async Task InstallTemplateAsync(this Bootstrapper bootstrapper, params string[] templates)
        {
            List<InstallRequest> installRequests = new List<InstallRequest>();
            foreach (string template in templates)
            {
                installRequests.Add(new InstallRequest(Path.GetFullPath(template)));
            }

            IReadOnlyList<InstallResult> installationResults = await bootstrapper.InstallTemplatePackagesAsync(installRequests).ConfigureAwait(false);
            if (installationResults.Any(result => !result.Success))
            {
                throw new Exception($"Failed to install templates: {string.Join(";", installationResults.Select(result => $"path: {result.InstallRequest.PackageIdentifier}, details:{result.ErrorMessage}"))}");
            }
        }
    }
}
