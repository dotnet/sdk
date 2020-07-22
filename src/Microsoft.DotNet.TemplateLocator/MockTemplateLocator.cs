// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Protocol;

namespace Microsoft.DotNet.TemplateLocator
{
    public class MockTemplateLocator : ITemplateLocator
    {
        private DirectoryInfo _dotnetSdkTemplatesLocation;

        public MockTemplateLocator()
        {
            string mockMockTemplateLocation = Environment.GetEnvironmentVariable("MOCKDOTNETSDKTEMPLATESLOCATION");
            if (!string.IsNullOrWhiteSpace(mockMockTemplateLocation))
                _dotnetSdkTemplatesLocation = new DirectoryInfo(mockMockTemplateLocation);
        }

        public IReadOnlyCollection<IOptionalSdkTemplatePackageInfo> GetDotnetSdkTemplatePackages(string sdkVersion)
        {
            if (_dotnetSdkTemplatesLocation == null) return Array.Empty<IOptionalSdkTemplatePackageInfo>();

            return LocalFolderUtility
                .GetPackagesV2(_dotnetSdkTemplatesLocation.FullName, new NullLogger())
                .Select(l => new MockOptionalSdkTemplatePackageInfo(l)).ToArray();
        }

        public string DotnetSdkVersionUsedInVs()
        {
            return "5.1.100";
        }

        public void SetDotnetSdkTemplatesLocation(DirectoryInfo directoryInfo)
        {
            _dotnetSdkTemplatesLocation = directoryInfo;
        }

        private class MockOptionalSdkTemplatePackageInfo : IOptionalSdkTemplatePackageInfo
        {
            public MockOptionalSdkTemplatePackageInfo(LocalPackageInfo localPackageInfo)
            {
                TemplatePackageId = localPackageInfo.Identity.Id;
                TemplateVersion = localPackageInfo.Identity.Version.ToNormalizedString();
                Path = localPackageInfo.Path;
            }

            public string TemplatePackageId { get; }

            public string TemplateVersion { get; }

            public string Path { get; }
        }
    }
}
