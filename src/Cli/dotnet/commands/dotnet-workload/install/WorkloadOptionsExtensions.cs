// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadOptionsExtensions
    {
        internal static ReleaseVersion GetValidatedSdkVersion(string versionOption, string providedVersion, string dotnetPath, string userProfileDir, bool checkIfFeatureBandManifestsExist)
        {

            if (string.IsNullOrEmpty(versionOption))
            {
                return new ReleaseVersion(providedVersion ?? Product.Version);
            }
            else
            {
                var manifests = new SdkDirectoryWorkloadManifestProvider(dotnetPath, versionOption, userProfileDir).GetManifests();
                if (!manifests.Any() && checkIfFeatureBandManifestsExist)
                {
                    throw new GracefulException(string.Format(LocalizableStrings.NoManifestsExistForFeatureBand, new SdkFeatureBand(versionOption).ToString()), isUserError: false);
                }
                try
                {
                    foreach (var readableManifest in manifests)
                    {
                        using (var manifestStream = readableManifest.OpenManifestStream())
                        using (var localizationStream = readableManifest.OpenLocalizationStream())
                        {
                            var manifest = WorkloadManifestReader.ReadWorkloadManifest(readableManifest.ManifestId, manifestStream, localizationStream, readableManifest.ManifestPath);
                        }
                    }
                }
                catch
                {
                    throw new GracefulException(string.Format(LocalizableStrings.IncompatibleManifests, versionOption), isUserError: false);
                }

                return new ReleaseVersion(versionOption);
            }
        }
    }
}
