// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ProgressDescriptionTests
{
    /// <summary>
    /// Simple <see cref="IProgressTask"/> backing store with no side effects,
    /// used to test <see cref="ShimmerProgressTask"/> without Spectre.Console.
    /// </summary>
    private sealed class PlainProgressTask(string description, double maxValue = 100) : IProgressTask
    {
        public string Description { get; set; } = description;
        public double Value { get; set; }
        public double MaxValue { get; set; } = maxValue;
    }

    [Fact]
    public void ShimmerProgressTask_StopShimmer_PreservesExternallySetDescription()
    {
        // Arrange: create a shimmer task with "Installing ..." description
        var inner = new PlainProgressTask("Installing SDK 11.0.100-preview.3");
        var shimmer = new ShimmerProgressTask(inner);

        // Let the shimmer timer fire at least once
        Thread.Sleep(150);

        // Value reaches 100% — triggers StopShimmer automatically
        shimmer.Value = 100;
        shimmer.Description.Should().Be("Installing SDK 11.0.100-preview.3",
            "StopShimmer should restore _baseDescription after auto-stop");

        // CompleteExtraction sets "Installed ..." via the Description setter
        shimmer.Description = "Installed SDK 11.0.100-preview.3";
        shimmer.Description.Should().Be("Installed SDK 11.0.100-preview.3");

        // Reporter.Dispose() calls StopShimmer() again — must be idempotent
        shimmer.Dispose();
        inner.Description.Should().Be("Installed SDK 11.0.100-preview.3",
            "StopShimmer on Dispose must preserve the last externally-set description, not revert to construction-time text");
    }

    [Fact]
    public void ShimmerProgressTask_NoShimmerForNonInstallingDescriptions()
    {
        // Shimmer only activates for descriptions starting with "Installing"
        var inner = new PlainProgressTask("Downloading SDK 11.0.100");
        using var shimmer = new ShimmerProgressTask(inner);

        Thread.Sleep(150);

        // Description should remain unchanged — no shimmer markup injected
        shimmer.Description.Should().Be("Downloading SDK 11.0.100");
    }

    [Fact]
    public void ShimmerProgressTask_DescriptionSetter_SyncsBaseDescription()
    {
        var inner = new PlainProgressTask("Installing SDK 11.0.100");
        var shimmer = new ShimmerProgressTask(inner);

        // Stop shimmer first (simulates value reaching 100%)
        shimmer.Value = 100;

        // Set new description (simulates CompleteExtraction)
        shimmer.Description = "Installed SDK 11.0.100";

        // Dispose calls StopShimmer again — must preserve "Installed"
        shimmer.Dispose();
        inner.Description.Should().Be("Installed SDK 11.0.100");
    }

    [Fact]
    public void ExtractorProgressTracker_CompleteExtraction_SetsInstalledDescription()
    {
        var recorder = new RecordingProgressReporter();
        var tracker = new ExtractorProgressTracker(recorder, InstallComponent.SDK, "11.0.100-preview.3.26170.106");

        var extractionTask = tracker.BeginExtraction();
        extractionTask.Description.Should().Contain(ProgressFormatting.ActionInstalling);

        extractionTask.Value = 100;
        tracker.CompleteExtraction(extractionTask);

        extractionTask.Description.Should().Contain(ProgressFormatting.ActionInstalled);
        extractionTask.Description.Should().Contain("11.0.100-preview.3");
        extractionTask.Description.Should().NotContain(ProgressFormatting.ActionInstalling);
    }

    [Theory]
    [InlineData(InstallComponent.SDK, "11.0.100-preview.3.26170.106")]
    [InlineData(InstallComponent.Runtime, "10.0.5")]
    [InlineData(InstallComponent.ASPNETCore, "9.0.312")]
    public void ExtractorProgressTracker_FullLifecycle_EndsWithInstalledDescription(
        InstallComponent component, string version)
    {
        var recorder = new RecordingProgressReporter();
        var tracker = new ExtractorProgressTracker(recorder, component, version);

        var task = tracker.BeginExtraction();
        task.Description.Should().Contain(ProgressFormatting.ActionInstalling);

        task.Value = 50;
        task.Value = 100;

        tracker.CompleteExtraction(task);
        string finalDescription = task.Description;
        finalDescription.Should().Contain(ProgressFormatting.ActionInstalled);
        finalDescription.Should().NotContain(ProgressFormatting.ActionInstalling);
    }

    private sealed class RecordingProgressReporter : IProgressReporter
    {
        public void Dispose() { }

        public IProgressTask AddTask(string description, double maxValue)
            => new PlainProgressTask(description, maxValue);
    }
}
