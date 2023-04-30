// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.NugetSearch;
using Microsoft.DotNet.ToolPackage;
using Xunit;

namespace dotnet.Tests.ToolSearchTests
{
    public class NugetSearchApiDeserializerTests
    {
        [Fact]
        public void ItCanDeserialize()
        {
            var json = File.ReadAllText("queryResultSample.json");

            var result = NugetSearchApiResultDeserializer.Deserialize(json);

            var firstItem = result.First();
            firstItem.Authors.Should().ContainSingle("author is a scalar");
            firstItem.Description.Should().Be("test app");
            firstItem.Id.Should().Be(new PackageId("global.tool.console.demo"));
            firstItem.LatestVersion.Should().Be("1.0.4");
            firstItem.Summary.Should().Be("The summary");
            firstItem.Tags.Should().ContainSingle("test");
            firstItem.TotalDownloads.Should().Be(20);
            firstItem.Verified.Should().BeFalse();
            firstItem.Versions.Single().Downloads.Should().Be(20);
            firstItem.Versions.Single().Version.Should().Be("1.0.4");

            var secondItem = result.Skip(1).First();
            secondItem.Authors.Should().ContainInOrder("author1", "authors2");
        }
    }
}
