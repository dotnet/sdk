// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class InvocationLifetimeOwnerSelectorTests
{
    [TestMethod]
    public void NormalizeHandlersTrimsDeduplicatesAndReturnsNullWhenEmpty()
    {
        InvocationLifetimeOwnerSelector.NormalizeHandlers([" B ", "A", "B", ""]).Should().Equal("A", "B");
        InvocationLifetimeOwnerSelector.NormalizeHandlers(["  "]).Should().BeNull();
    }

    [TestMethod]
    public void SelectReturnsNotRequestedWhenNoModuleDeclaresHandlers()
    {
        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select([CreateModule("A"), CreateModule("B")]);

        selection.Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.NotRequested);
        selection.Owner.Should().BeNull();
    }

    [TestMethod]
    public void SelectPrefersTheSingleExplicitOwner()
    {
        TestModule first = CreateModule("A", ["Sql", "Blob"]);
        TestModule explicitOwner = CreateModule("B", ["Sql"], isOwner: true);

        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select([first, explicitOwner]);

        selection.Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.Selected);
        selection.Owner.Should().BeSameAs(explicitOwner);
    }

    [TestMethod]
    public void SelectUsesTheFirstSupersetModuleWhenExplicitOwnerIsNotUnique()
    {
        TestModule noHandlers = CreateModule("A");
        TestModule subset = CreateModule("B", ["Sql"], isOwner: true);
        TestModule superset = CreateModule("C", ["Sql", "Blob"], isOwner: true);
        TestModule laterSuperset = CreateModule("D", ["Blob", "Sql"]);

        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select([noHandlers, subset, superset, laterSuperset]);

        selection.Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.Selected);
        selection.Owner.Should().BeSameAs(superset);
    }

    [TestMethod]
    public void SelectReturnsAmbiguousWhenNoModuleCoversAllHandlers()
    {
        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select(
            [CreateModule("A", ["Sql"]), CreateModule("B", ["Blob"])]);

        selection.Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.Ambiguous);
        selection.Owner.Should().BeNull();
    }

    [TestMethod]
    public void SelectIgnoresModulesThatAreNotTestingPlatformApplications()
    {
        TestModule vstest = CreateModule("A", ["Sql", "Blob"], isTestingPlatformApplication: false);
        TestModule mtp = CreateModule("B", ["Sql"]);

        InvocationLifetimeOwnerSelector.Select([vstest, mtp]).Owner.Should().BeSameAs(mtp);
    }

    [TestMethod]
    public void SelectRejectsAnOwnerThatRequiresTheHttpTransport()
    {
        TestModule browserOwner = CreateModule("A", ["Sql"], runtimeIdentifier: "browser-wasm");

        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select([browserOwner, CreateModule("B")]);

        selection.Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.UnsupportedTransport);
        selection.Owner.Should().BeSameAs(browserOwner);
    }

    [TestMethod]
    public void SelectOrdersCandidatesByProjectPathRegardlessOfInputOrder()
    {
        TestModule first = CreateModule("A", ["Sql"]);
        TestModule second = CreateModule("B", ["Sql"]);

        InvocationLifetimeOwnerSelector.Select([second, first]).Owner.Should().BeSameAs(first);
    }

    [TestMethod]
    public void SelectReturnsNotRequestedWithoutHandlersEvenIfNoModuleSupportsCoordination()
    {
        InvocationLifetimeOwnerSelector.Select([CreateModule("A", supportsCoordination: false), CreateModule("B", supportsCoordination: false)])
            .Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.NotRequested);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Handler-bearing participant")]
    [DataRow(false, DisplayName = "Participant without declared handlers (handlers may be code-only)")]
    public void SelectFallsBackWhenAnyParticipantDoesNotSupportCoordination(bool participantDeclaresHandlers)
    {
        TestModule owner = CreateModule("A", ["Sql"], isOwner: true);
        TestModule legacy = CreateModule("B", participantDeclaresHandlers ? ["Sql"] : null, supportsCoordination: false);

        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select([owner, legacy]);

        selection.Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.UnsupportedParticipant);
        selection.Owner.Should().BeNull();
        selection.UnsupportedParticipant.Should().BeSameAs(legacy);
    }

    [TestMethod]
    public void SelectFallsBackWhenOnlyOneTargetFrameworkOfAParticipantDoesNotSupportCoordination()
    {
        TestModule owner = CreateModule("A", ["Sql"]);
        TestModule current = CreateModule("B", targetFramework: "net10.0");
        TestModule legacy = CreateModule("B", targetFramework: "net8.0", supportsCoordination: false);

        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select([owner, current, legacy]);

        selection.Status.Should().Be(InvocationLifetimeOwnerSelectionStatus.UnsupportedParticipant);
        selection.UnsupportedParticipant.Should().BeSameAs(legacy);
    }

    [TestMethod]
    public void SelectIgnoresCoordinationSupportOfModulesThatAreNotTestingPlatformApplications()
    {
        TestModule owner = CreateModule("A", ["Sql"]);
        TestModule vstest = CreateModule("B", isTestingPlatformApplication: false, supportsCoordination: false);

        InvocationLifetimeOwnerSelector.Select([owner, vstest]).Owner.Should().BeSameAs(owner);
    }

    internal static TestModule CreateModule(
        string name,
        string[]? handlers = null,
        bool isOwner = false,
        bool isTestingPlatformApplication = true,
        string runtimeIdentifier = "win-x64",
        bool supportsCoordination = true,
        string targetFramework = "net10.0")
    {
        string targetPath = Path.Combine(Path.GetTempPath(), name, targetFramework, $"{name}.dll");
        return new TestModule(
            new RunProperties(
                Command: "dotnet",
                Arguments: targetPath,
                WorkingDirectory: Path.GetDirectoryName(targetPath),
                RuntimeIdentifier: runtimeIdentifier,
                DefaultAppHostRuntimeIdentifier: "win-x64",
                TargetFrameworkVersion: "v10.0"),
            ProjectFullPath: Path.Combine(Path.GetTempPath(), name, $"{name}.csproj"),
            TargetFramework: targetFramework,
            IsTestingPlatformApplication: isTestingPlatformApplication,
            LaunchSettings: null,
            TargetPath: targetPath,
            DotnetRootArchVariableName: null,
            EnvironmentVariables: ImmutableDictionary<string, string>.Empty,
            InvocationLifetimeHandlers: handlers is null ? null : InvocationLifetimeOwnerSelector.NormalizeHandlers(handlers),
            IsInvocationLifetimeOwner: isOwner,
            SupportsInvocationLifetimeCoordination: supportsCoordination);
    }
}
