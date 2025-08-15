// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockWorkloadResolverFactory : IWorkloadResolverFactory
    {
        public IWorkloadResolverFactory.CreationResult MockResult { get; set; } = new();

        public IWorkloadResolverFactory.CreationResult Create(string globalJsonStartDir = null) => MockResult;
        public IWorkloadResolver CreateForWorkloadSet(string dotnetPath, string sdkVersion, string userProfileDir, string workloadSetVersion)
        {
            if (dotnetPath != MockResult.DotnetPath ||
                sdkVersion != MockResult.SdkVersion.ToString() ||
                userProfileDir != MockResult.UserProfileDir ||
                workloadSetVersion != null)
            {
                throw new NotImplementedException("Workload resolver factory mock does not support argument.");
            }
            return MockResult.WorkloadResolver;
        }

        public MockWorkloadResolverFactory()
        {
        }

        public MockWorkloadResolverFactory(string dotnetPath, string sdkVersion, IWorkloadResolver workloadResolver, string userProfileDir = null)
        {
            MockResult.DotnetPath = dotnetPath;
            MockResult.SdkVersion = new ReleaseVersion(sdkVersion);
            MockResult.WorkloadResolver = workloadResolver;
            MockResult.UserProfileDir = userProfileDir;
        }
    }
}
