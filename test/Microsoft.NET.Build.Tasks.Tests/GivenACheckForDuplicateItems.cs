// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenACheckForDuplicateItems
    {
        [TestMethod]
        public void CheckForNoDuplicateItems()
        {
            var compile = new[]
            {
                new TaskItem("foo.cs"),
                new TaskItem("bar.cs"),
            };

            // execute task
            var task = new CheckForDuplicateItems()
            {
                Items = compile,
                ItemName = "Compile",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeTrue();

            task.DeduplicatedItems.Length.Should().Be(0);
        }

        [TestMethod]
        public void CheckForDuplicateItems()
        {
            var compile = new[]
            {
                new TaskItem("foo.cs"),
                new TaskItem("FOO.cs"),
            };
            var engine = new MockBuildEngine();

            // execute task
            var task = new CheckForDuplicateItems()
            {
                BuildEngine = engine,
                Items = compile,
                ItemName = "Compile",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeFalse();

            engine.Errors.Count.Should().Be(1);
            engine.Errors[0].Code.Should().Be("NETSDK1022");
            engine.Errors[0].Message.Should().EndWith("The duplicate items were: 'foo.cs'");

            task.DeduplicatedItems.Length.Should().Be(1);
        }

        [TestMethod]
        public void CheckForNoDuplicateItemsWhenDistinguishingMetadataIsDifferent()
        {
            var embeddedResources = new[]
            {
                new TaskItem("image.bmp", new Dictionary<string, string> { { "LogicalName", "First.bmp" } }),
                new TaskItem("image.bmp", new Dictionary<string, string> { { "LogicalName", "Second.bmp" } }),
            };
            var engine = new MockBuildEngine();

            // execute task
            var task = new CheckForDuplicateItems()
            {
                BuildEngine = engine,
                Items = embeddedResources,
                ItemName = "EmbeddedResource",
                DistinguishingMetadataName = "LogicalName",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeTrue();

            engine.Errors.Count.Should().Be(0);
            task.DeduplicatedItems.Length.Should().Be(0);
        }

        [TestMethod]
        public void CheckForDuplicateItemsWhenDistinguishingMetadataIsMissing()
        {
            var embeddedResources = new[]
            {
                new TaskItem("Strings.resx"),
                new TaskItem("Strings.resx", new Dictionary<string, string> { { "LogicalName", "MyStrings.resources" } }),
            };
            var engine = new MockBuildEngine();

            // execute task
            var task = new CheckForDuplicateItems()
            {
                BuildEngine = engine,
                Items = embeddedResources,
                ItemName = "EmbeddedResource",
                DistinguishingMetadataName = "LogicalName",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeFalse();

            engine.Errors.Count.Should().Be(1);
            engine.Errors[0].Message.Should().EndWith("The duplicate items were: 'Strings.resx'");
        }

        [TestMethod]
        public void CheckForDuplicateItemsWhenDistinguishingMetadataIsTheSame()
        {
            var embeddedResources = new[]
            {
                new TaskItem("image.bmp", new Dictionary<string, string> { { "LogicalName", "Same.bmp" } }),
                new TaskItem("image.bmp", new Dictionary<string, string> { { "LogicalName", "Same.bmp" } }),
            };
            var engine = new MockBuildEngine();

            // execute task
            var task = new CheckForDuplicateItems()
            {
                BuildEngine = engine,
                Items = embeddedResources,
                ItemName = "EmbeddedResource",
                DistinguishingMetadataName = "LogicalName",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeFalse();

            engine.Errors.Count.Should().Be(1);
            engine.Errors[0].Message.Should().EndWith("The duplicate items were: 'image.bmp'");
        }

        [TestMethod]
        public void DeduplicatedItemsKeepItemsWithDifferentDistinguishingMetadata()
        {
            var embeddedResources = new[]
            {
                new TaskItem("image.bmp", new Dictionary<string, string> { { "LogicalName", "First.bmp" } }),
                new TaskItem("image.bmp", new Dictionary<string, string> { { "LogicalName", "Second.bmp" } }),
                new TaskItem("Strings.resx"),
                new TaskItem("STRINGS.resx"),
            };
            var engine = new MockBuildEngine();

            // execute task
            var task = new CheckForDuplicateItems()
            {
                BuildEngine = engine,
                Items = embeddedResources,
                ItemName = "EmbeddedResource",
                DistinguishingMetadataName = "LogicalName",
                PropertyNameToDisableDefaultItems = "PropertyNameToDisableDefaultItems",
                MoreInformationLink = "MoreInformationLink",
                DefaultItemsEnabled = true,
                DefaultItemsOfThisTypeEnabled = true
            };
            task.Execute().Should().BeFalse();

            engine.Errors.Count.Should().Be(1);
            engine.Errors[0].Message.Should().EndWith("The duplicate items were: 'Strings.resx'");

            task.DeduplicatedItems.Select(i => i.GetMetadata("LogicalName")).Should().BeEquivalentTo("First.bmp", "Second.bmp", "");
        }
    }
}
