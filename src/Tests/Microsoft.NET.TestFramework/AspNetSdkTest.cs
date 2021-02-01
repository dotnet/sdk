// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;
using NuGet.Configuration;

namespace Microsoft.NET.TestFramework
{
    public abstract class AspNetSdkTest : SdkTest
    {
        private readonly IEnumerable<System.Reflection.AssemblyMetadataAttribute> TestAssemblyMetadata;
        public readonly string DefaultTfm;

        private List<string> NuGetFeedsRetained = new List<string>(){"dotnet-public", "dotnet6", "dotnet6-transport"};

        protected AspNetSdkTest(ITestOutputHelper log) : base(log) {
            var assembly = Assembly.GetCallingAssembly();
            TestAssemblyMetadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            DefaultTfm = TestAssemblyMetadata.SingleOrDefault(a => a.Key == "AspNetTestTfm").Value;
        }

        private void CopyAndModifyNugetConfig(string projectDirectory)
        {
            // Copy the config at the root of the repo to `projectDirectory`
            var repoRoot = TestAssemblyMetadata.SingleOrDefault(a => a.Key == "RepoRoot").Value;
            var configAtRoot = Path.Combine(repoRoot, "nuget.config");
            File.Copy(configAtRoot, Path.Combine(projectDirectory, "nuget.config"));

            // Remove sources not in `NugetFeedsRetained`
            var nugetConfig = Settings.LoadSpecificSettings(projectDirectory, "nuget.config");
            var packageSourcesSection =  nugetConfig.GetSection(ConfigurationConstants.PackageSources);
            var sources = packageSourcesSection?.Items.OfType<SourceItem>();

            foreach (var source in sources)
            {
                if (!NuGetFeedsRetained.Contains(source.Key))
                {
                    nugetConfig.Remove(ConfigurationConstants.PackageSources, source);
                }
            }

            nugetConfig.SaveToDisk();
        }

        public TestAsset CreateAspNetSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null) 
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory)
                .WithSource()
                .WithProjectChanges(project => 
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFramework");
                    if (targetFramework.Value == "$(AspNetTestTfm)")
                    {
                        targetFramework.Value = overrideTfm ?? DefaultTfm;
                    }
                });
            CopyAndModifyNugetConfig(projectDirectory.TestRoot);
            return projectDirectory;
        }
    }
}
