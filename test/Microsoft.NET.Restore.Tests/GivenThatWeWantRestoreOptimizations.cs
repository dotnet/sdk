// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Commands.Build;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantRestoreOptimizations : SdkTest
    {
        public GivenThatWeWantRestoreOptimizations(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_adds_EnableDefaultItems_false_by_default()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "MyProject.csproj" };

            // Act
            var optimizedArgs = Constants.AddRestoreOptimizations(originalArgs);

            // Assert
            optimizedArgs.Should().Contain($"-property:{Constants.EnableDefaultItems}=false");
        }

        [Fact]
        public void It_does_not_add_EnableDefaultItems_false_when_user_specified_EnableDefaultItems_true()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "-property:EnableDefaultItems=true", "MyProject.csproj" };

            // Act
            var optimizedArgs = Constants.AddRestoreOptimizations(originalArgs);

            // Assert
            optimizedArgs.Should().NotContain($"-property:{Constants.EnableDefaultItems}=false");
            optimizedArgs.Should().Contain("-property:EnableDefaultItems=true");
        }

        [Fact]
        public void It_does_not_add_EnableDefaultItems_false_when_user_specified_EnableDefaultItems_false()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "-property:EnableDefaultItems=false", "MyProject.csproj" };

            // Act
            var optimizedArgs = Constants.AddRestoreOptimizations(originalArgs);

            // Assert
            var enableDefaultItemsArgs = optimizedArgs.Where(arg => arg.Contains("EnableDefaultItems")).ToList();
            enableDefaultItemsArgs.Should().ContainSingle();
            enableDefaultItemsArgs.Single().Should().Be("-property:EnableDefaultItems=false");
        }

        [Fact]
        public void It_respects_user_EnableDefaultItems_with_short_property_syntax()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "-p:EnableDefaultItems=true", "MyProject.csproj" };

            // Act
            var optimizedArgs = Constants.AddRestoreOptimizations(originalArgs);

            // Assert
            optimizedArgs.Should().NotContain($"-property:{Constants.EnableDefaultItems}=false");
            optimizedArgs.Should().Contain("-p:EnableDefaultItems=true");
        }

        [Fact]
        public void It_respects_user_EnableDefaultItems_with_double_dash_syntax()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "--property:EnableDefaultItems=true", "MyProject.csproj" };

            // Act
            var optimizedArgs = Constants.AddRestoreOptimizations(originalArgs);

            // Assert
            optimizedArgs.Should().NotContain($"-property:{Constants.EnableDefaultItems}=false");
            optimizedArgs.Should().Contain("--property:EnableDefaultItems=true");
        }

        [Fact]
        public void It_handles_case_insensitive_property_names()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "-property:enabledefaultitems=true", "MyProject.csproj" };

            // Act
            var optimizedArgs = Constants.AddRestoreOptimizations(originalArgs);

            // Assert
            optimizedArgs.Should().NotContain($"-property:{Constants.EnableDefaultItems}=false");
            optimizedArgs.Should().Contain("-property:enabledefaultitems=true");
        }

        [Fact]
        public void RestoreCommand_CreateForwarding_includes_optimization()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "MyProject.csproj" };

            // Act
            var restoreCommand = RestoreCommand.CreateForwarding(originalArgs);

            // Assert
            var msbuildArgs = restoreCommand.MSBuildArguments;
            msbuildArgs.Should().Contain($"-property:{Constants.EnableDefaultItems}=false");
        }

        [Fact]
        public void RestoreCommand_CreateForwarding_respects_user_EnableDefaultItems()
        {
            // Arrange
            var originalArgs = new[] { "-target:Restore", "-property:EnableDefaultItems=true", "MyProject.csproj" };

            // Act
            var restoreCommand = RestoreCommand.CreateForwarding(originalArgs);

            // Assert
            var msbuildArgs = restoreCommand.MSBuildArguments;
            msbuildArgs.Should().NotContain($"-property:{Constants.EnableDefaultItems}=false");
            msbuildArgs.Should().Contain("-property:EnableDefaultItems=true");
        }

        [Fact]
        public void SeparateRestoreCommand_includes_optimization_when_needed()
        {
            // This tests the optimization in the separate restore command path
            // which is used when certain properties are excluded from the main restore
            
            // Arrange - use args that trigger separate restore (like specifying TargetFramework)
            var args = new[] { "-f", "net6.0", "MyProject.csproj" };

            // Act
            var buildCommand = (RestoringCommand)BuildCommand.FromArgs(args, "msbuildpath");

            // Assert
            buildCommand.SeparateRestoreCommand.Should().NotBeNull();
            var restoreArgs = buildCommand.SeparateRestoreCommand.MSBuildArguments;
            restoreArgs.Should().Contain($"-property:{Constants.EnableDefaultItems}=false");
        }

        [Fact]
        public void SeparateRestoreCommand_respects_user_EnableDefaultItems()
        {
            // Arrange - use args that trigger separate restore and specify EnableDefaultItems
            var args = new[] { "-f", "net6.0", "-property:EnableDefaultItems=true", "MyProject.csproj" };

            // Act
            var buildCommand = (RestoringCommand)BuildCommand.FromArgs(args, "msbuildpath");

            // Assert
            buildCommand.SeparateRestoreCommand.Should().NotBeNull();
            var restoreArgs = buildCommand.SeparateRestoreCommand.MSBuildArguments;
            restoreArgs.Should().NotContain($"-property:{Constants.EnableDefaultItems}=false");
            restoreArgs.Should().Contain("-property:EnableDefaultItems=true");
        }
    }
}