// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class CheckForUnsupportedWindowsTargetPlatformVersionTests
    {
        private readonly MockTaskItem[] _knownFrameworkReferences = {
            new MockTaskItem("Microsoft.Windows.SDK.NET.Ref",
                new Dictionary<string, string>
                {
                    {"TargetFramework", "net5.0-windows10.0.17760"},
                    {"RuntimeFrameworkName", "Microsoft.Windows.SDK.NET.Ref"},
                    {"DefaultRuntimeFrameworkVersion", "10.0.17760.1-preview"},
                    {"LatestRuntimeFrameworkVersion", "10.0.17760.1-preview"},
                    {"TargetingPackName", "Microsoft.Windows.SDK.NET.Ref"},
                    {"TargetingPackVersion", "10.0.17760.1-preview"},
                    {"RuntimePackNamePatterns", "Microsoft.Windows.SDK.NET.Ref"},
                    {"RuntimePackRuntimeIdentifiers", "any"},
                    {MetadataKeys.RuntimePackAlwaysCopyLocal, "true"},
                    {"IsWindowsOnly", "true"},
                }),
            new MockTaskItem("Microsoft.Windows.SDK.NET.Ref",
                new Dictionary<string, string>
                {
                    {"TargetFramework", "net5.0-windows10.0.18362"},
                    {"RuntimeFrameworkName", "Microsoft.Windows.SDK.NET.Ref"},
                    {"DefaultRuntimeFrameworkVersion", "10.0.18362.1-preview"},
                    {"LatestRuntimeFrameworkVersion", "10.0.18362.1-preview"},
                    {"TargetingPackName", "Microsoft.Windows.SDK.NET.Ref"},
                    {"TargetingPackVersion", "10.0.18362.1-preview"},
                    {"RuntimePackNamePatterns", "Microsoft.Windows.SDK.NET.Ref"},
                    {"RuntimePackRuntimeIdentifiers", "any"},
                    {MetadataKeys.RuntimePackAlwaysCopyLocal, "true"},
                    {"IsWindowsOnly", "true"},
                }),
            new MockTaskItem("Microsoft.NETCore.App",
                new Dictionary<string, string>
                {
                    {"TargetFramework", "net5.0"},
                    {"RuntimeFrameworkName", "Microsoft.NETCore.App"},
                    {"DefaultRuntimeFrameworkVersion", "5.0.0-preview.4.20251.6"},
                    {"LatestRuntimeFrameworkVersion", "5.0.0-preview.4.20251.6"},
                    {"TargetingPackName", "Microsoft.NETCore.App.Ref"},
                    {"TargetingPackVersion", "5.0.0-preview.4.20251.6"},
                    {"RuntimePackNamePatterns", "Microsoft.NETCore.App.Runtime.**RID**"},
                    {"RuntimePackRuntimeIdentifiers", "win-x64"},
                }),
        };

        private readonly MockNeverCacheBuildEngine4 _mockBuildEngine;
        private readonly CheckForUnsupportedWindowsTargetPlatformVersion _taskWithDefaultParameter;

        public CheckForUnsupportedWindowsTargetPlatformVersionTests()
        {
            _mockBuildEngine = new MockNeverCacheBuildEngine4();
            _taskWithDefaultParameter = new CheckForUnsupportedWindowsTargetPlatformVersion
            {
                BuildEngine = _mockBuildEngine,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                KnownFrameworkReferences = _knownFrameworkReferences,
                WinRTApisPackageName = "Microsoft.Windows.SDK.NET.Ref"
            };
        }

        [Fact]
        public void Given_matching_TargetPlatformVersion_it_should_pass()
        {
            _taskWithDefaultParameter.Execute().Should().BeTrue();
        }

        [Fact]
        public void Given_no_matching_TargetPlatformVersion_it_should_error()
        {
            _taskWithDefaultParameter.TargetPlatformVersion = "10.0.9999";

            _taskWithDefaultParameter.Execute().Should().BeFalse();
            var actualErrorMessage = _mockBuildEngine.Errors.First().Message;
            string.Format(Strings.InvalidTargetPlatformVersion, "10.0.9999", "Windows", "10.0.17760 10.0.18362")
                .Should().Contain(actualErrorMessage);
        }
    }
}
