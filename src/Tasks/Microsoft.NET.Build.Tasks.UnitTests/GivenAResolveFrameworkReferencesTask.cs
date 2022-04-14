// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveFrameworkReferences
    {
        [Fact]
        public void It_resolves_with_multiple_runtime_packs()
        {
            var task = new ResolveFrameworkReferences
            {
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()),
                    new MockTaskItem("Microsoft.Android", new Dictionary<string, string>()),
                },

                ResolvedTargetingPacks = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App",
                        new Dictionary<string, string>()
                        {
                            {"NuGetPackageId", "Microsoft.NETCore.App.Ref"},
                            {"NuGetPackageVersion", "6.0.2"},
                            {"Path", "empty"},
                        }),
                    new MockTaskItem("Microsoft.Android",
                        new Dictionary<string, string>()
                        {
                            {"NuGetPackageId", "Microsoft.Android.Ref.32"},
                            {"NuGetPackageVersion", "32.0.300"},
                            {"Path", "empty"},
                            {"Profile", "Android"},
                        }),
                },

                ResolvedRuntimePacks = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App.Runtime.Mono.android-arm",
                        new Dictionary<string, string>()
                        {
                            {"FrameworkName", "Microsoft.NetCore.App"},
                            {"NuGetPackageId", "Microsoft.NETCore.App.Runtime.Mono.android-arm"},
                            {"NuGetPackageVersion", "6.0.4"},
                            {"PackageDirectory", "empty"},
                        }),
                    new MockTaskItem("Microsoft.Android.Runtime.32",
                        new Dictionary<string, string>()
                        {
                            {"FrameworkName", "Microsoft.Android"},
                            {"NuGetPackageId", "Microsoft.Android.Runtime.32"},
                            {"NuGetPackageVersion", "32.0.300"},
                            {"PackageDirectory", "empty"},
                        }),
                    new MockTaskItem("Microsoft.Android.Runtime.32.android-arm",
                        new Dictionary<string, string>()
                        {
                            {"FrameworkName", "Microsoft.Android"},
                            {"NuGetPackageId", "Microsoft.Android.Runtime.32.android-arm"},
                            {"NuGetPackageVersion", "32.0.300"},
                            {"PackageDirectory", "empty"},
                        }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ResolvedFrameworkReferences.Length.Should().Be(2);
            task.ResolvedFrameworkReferences[0].GetMetadata("RuntimePackPath").Should().Be("empty");
            task.ResolvedFrameworkReferences[0].GetMetadata("RuntimePackName").Should().Be("Microsoft.NETCore.App.Runtime.Mono.android-arm");
            task.ResolvedFrameworkReferences[0].GetMetadata("RuntimePackVersion").Should().Be("6.0.4");
            task.ResolvedFrameworkReferences[1].GetMetadata("RuntimePackPath").Should().Be("empty;empty");
            task.ResolvedFrameworkReferences[1].GetMetadata("RuntimePackName").Should().Be("Microsoft.Android.Runtime.32;Microsoft.Android.Runtime.32.android-arm");
            task.ResolvedFrameworkReferences[1].GetMetadata("RuntimePackVersion").Should().Be("32.0.300;32.0.300");
        }
    }

}
