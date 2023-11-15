// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.Install
{

    internal interface IWorkloadResolverFactory
    {
        public class CreationResult
        {
            public string DotnetPath { get; set; }
            public string UserProfileDir { get; set; }
            public ReleaseVersion SdkVersion { get; set; }
            public IWorkloadResolver WorkloadResolver { get; set; }
        }

        CreationResult Create(string globalJsonStartDir = null);

        IWorkloadResolver CreateForWorkloadSet(string dotnetPath, string sdkVersion, string userProfileDir, string workloadSetVersion, bool useInstallStateOnly = false);
    }

    internal class WorkloadResolverFactory : IWorkloadResolverFactory
    {
        public IWorkloadResolverFactory.CreationResult Create(string globalJsonStartDir = null)
        {
            var result = new IWorkloadResolverFactory.CreationResult();

            result.SdkVersion = new ReleaseVersion(Product.Version);

            result.DotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
            result.UserProfileDir = CliFolderPathCalculator.DotnetUserProfileFolderPath;
            globalJsonStartDir = globalJsonStartDir ?? Environment.CurrentDirectory;

            string globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(globalJsonStartDir);

            var sdkWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(result.DotnetPath, result.SdkVersion.ToString(), result.UserProfileDir, globalJsonPath);

            result.WorkloadResolver = WorkloadResolver.Create(sdkWorkloadManifestProvider, result.DotnetPath, result.SdkVersion.ToString(), result.UserProfileDir);

            return result;
        }

        public IWorkloadResolver CreateForWorkloadSet(string dotnetPath, string sdkVersion, string userProfileDir, string workloadSetVersion, bool useInstallStateOnly = false)
        {
            var manifestProvider = SdkDirectoryWorkloadManifestProvider.ForWorkloadSet(dotnetPath, sdkVersion, userProfileDir, workloadSetVersion);
            return WorkloadResolver.Create(manifestProvider, dotnetPath, sdkVersion, userProfileDir, useInstallStateOnly: useInstallStateOnly);
        }
    }
}
