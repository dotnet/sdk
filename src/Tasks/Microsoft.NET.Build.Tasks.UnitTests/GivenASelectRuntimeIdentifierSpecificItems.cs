// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenASelectRuntimeIdentifierSpecificItems
    {
        [Fact]
        public void ItSelectsCompatibleItems()
        {
            // Arrange
            var testRuntimeGraphPath = CreateTestRuntimeGraph();
            var items = new[]
            {
                CreateTaskItem("Item1", "linux-x64"),
                CreateTaskItem("Item2", "win-x64"),
                CreateTaskItem("Item3", "linux"),
                CreateTaskItem("Item4", "ubuntu.18.04-x64")
            };

            var task = new SelectRuntimeIdentifierSpecificItems()
            {
                TargetRuntimeIdentifier = "ubuntu.18.04-x64",
                Items = items,
                RuntimeIdentifierGraphPath = testRuntimeGraphPath,
                BuildEngine = new MockBuildEngine()
            };

            // Act
            bool result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.SelectedItems.Should().HaveCount(3); // linux-x64, linux, ubuntu.18.04-x64 should be compatible
            task.SelectedItems.Should().Contain(i => i.ItemSpec == "Item1"); // linux-x64
            task.SelectedItems.Should().Contain(i => i.ItemSpec == "Item3"); // linux
            task.SelectedItems.Should().Contain(i => i.ItemSpec == "Item4"); // ubuntu.18.04-x64
            task.SelectedItems.Should().NotContain(i => i.ItemSpec == "Item2"); // win-x64
        }

        [Fact]
        public void ItSelectsItemsWithExactMatch()
        {
            // Arrange
            var testRuntimeGraphPath = CreateTestRuntimeGraph();
            var items = new[]
            {
                CreateTaskItem("Item1", "win-x64"),
                CreateTaskItem("Item2", "linux-x64")
            };

            var task = new SelectRuntimeIdentifierSpecificItems()
            {
                TargetRuntimeIdentifier = "win-x64",
                Items = items,
                RuntimeIdentifierGraphPath = testRuntimeGraphPath,
                BuildEngine = new MockBuildEngine()
            };

            // Act
            bool result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.SelectedItems.Should().HaveCount(1);
            task.SelectedItems[0].ItemSpec.Should().Be("Item1");
        }

        [Fact]
        public void ItSkipsItemsWithoutRuntimeIdentifierMetadata()
        {
            // Arrange
            var testRuntimeGraphPath = CreateTestRuntimeGraph();
            var items = new[]
            {
                CreateTaskItem("Item1", "linux-x64"),
                CreateTaskItem("Item2", null), // No runtime identifier
                CreateTaskItem("Item3", "") // Empty runtime identifier
            };

            var task = new SelectRuntimeIdentifierSpecificItems()
            {
                TargetRuntimeIdentifier = "linux-x64",
                Items = items,
                RuntimeIdentifierGraphPath = testRuntimeGraphPath,
                BuildEngine = new MockBuildEngine()
            };

            // Act
            bool result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.SelectedItems.Should().HaveCount(1);
            task.SelectedItems[0].ItemSpec.Should().Be("Item1");
        }

        [Fact]
        public void ItUsesCustomRuntimeIdentifierMetadata()
        {
            // Arrange
            var testRuntimeGraphPath = CreateTestRuntimeGraph();
            var item = new TaskItem("Item1");
            item.SetMetadata("CustomRID", "linux-x64");

            var task = new SelectRuntimeIdentifierSpecificItems()
            {
                TargetRuntimeIdentifier = "ubuntu.18.04-x64",
                Items = new[] { item },
                RuntimeIdentifierItemMetadata = "CustomRID",
                RuntimeIdentifierGraphPath = testRuntimeGraphPath,
                BuildEngine = new MockBuildEngine()
            };

            // Act
            bool result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.SelectedItems.Should().HaveCount(1);
            task.SelectedItems[0].ItemSpec.Should().Be("Item1");
        }

        [Fact]
        public void ItReturnsEmptyArrayWhenNoItemsProvided()
        {
            // Arrange
            var testRuntimeGraphPath = CreateTestRuntimeGraph();

            var task = new SelectRuntimeIdentifierSpecificItems()
            {
                TargetRuntimeIdentifier = "linux-x64",
                Items = new ITaskItem[0],
                RuntimeIdentifierGraphPath = testRuntimeGraphPath,
                BuildEngine = new MockBuildEngine()
            };

            // Act
            bool result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.SelectedItems.Should().BeEmpty();
        }

        private static TaskItem CreateTaskItem(string itemSpec, string? runtimeIdentifier)
        {
            var item = new TaskItem(itemSpec);
            if (!string.IsNullOrEmpty(runtimeIdentifier))
            {
                item.SetMetadata("RuntimeIdentifier", runtimeIdentifier);
            }
            return item;
        }

        private static string CreateTestRuntimeGraph()
        {
            // Create a minimal runtime graph for testing
            var runtimeGraph = @"{
  ""runtimes"": {
    ""linux"": {},
    ""linux-x64"": {
      ""#import"": [""linux""]
    },
    ""ubuntu"": {
      ""#import"": [""linux""]
    },
    ""ubuntu.18.04"": {
      ""#import"": [""ubuntu""]
    },
    ""ubuntu.18.04-x64"": {
      ""#import"": [""ubuntu.18.04"", ""linux-x64""]
    },
    ""win"": {},
    ""win-x64"": {
      ""#import"": [""win""]
    }
  }
}";

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, runtimeGraph);
            return tempFile;
        }
    }
}