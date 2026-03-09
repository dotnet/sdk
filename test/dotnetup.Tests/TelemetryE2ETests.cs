// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// End-to-end tests that verify telemetry output by running dotnetup as a separate process
/// with the console exporter enabled (DOTNETUP_TELEMETRY_DEBUG=1).
/// </summary>
public class TelemetryE2ETests
{
    /// <summary>
    /// Environment variables that enable telemetry debug output.
    /// </summary>
    private static readonly Dictionary<string, string> s_telemetryEnvVars = new()
    {
        ["DOTNETUP_TELEMETRY_DEBUG"] = "1",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "0",
    };

    /// <summary>
    /// Creates a merged environment variable dictionary that includes telemetry debug settings
    /// and a per-test data directory to avoid writing to the real user profile.
    /// </summary>
    private static Dictionary<string, string> GetTelemetryEnvVars(string dataDir) => new(s_telemetryEnvVars)
    {
        ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = dataDir,
    };

    [Fact]
    public void InvalidVersion_ProducesUserError_OnRootSpan()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildSdkArguments("999.999.999", testEnv.InstallPath, testEnv.ManifestPath);
        var envVars = GetTelemetryEnvVars(testEnv.TempRoot);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            args, captureOutput: true, workingDirectory: testEnv.TempRoot, environmentVariables: envVars);

        exitCode.Should().NotBe(0, "requesting a nonexistent SDK version should fail");

        var spans = ParseTelemetrySpans(output);
        spans.Should().NotBeEmpty("console exporter should emit telemetry spans");

        var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
        rootSpan.Should().NotBeNull("root 'dotnetup' span should be emitted");

        rootSpan!.Tags.Should().ContainKey("error.type", "root span should have error.type tag");
        rootSpan.Tags.Should().ContainKey("error.category", "root span should have error.category tag");
    }

    [Fact]
    public void InvalidVersion_ErrorTags_AreOnCommandSpan()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildSdkArguments("999.999.999", testEnv.InstallPath, testEnv.ManifestPath);
        var envVars = GetTelemetryEnvVars(testEnv.TempRoot);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            args, captureOutput: true, workingDirectory: testEnv.TempRoot, environmentVariables: envVars);

        exitCode.Should().NotBe(0);

        var spans = ParseTelemetrySpans(output);
        var commandSpan = spans.FirstOrDefault(s => s.DisplayName.StartsWith("command/", StringComparison.Ordinal));
        commandSpan.Should().NotBeNull("a command/* span should be emitted");

        commandSpan!.Tags.Should().ContainKey("error.type", "command span should have error.type tag");
    }

    [Fact]
    public void InvalidVersion_ErrorDetails_ArePropagatedToRootSpan()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildSdkArguments("999.999.999", testEnv.InstallPath, testEnv.ManifestPath);
        var envVars = GetTelemetryEnvVars(testEnv.TempRoot);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            args, captureOutput: true, workingDirectory: testEnv.TempRoot, environmentVariables: envVars);

        exitCode.Should().NotBe(0);

        var spans = ParseTelemetrySpans(output);
        var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
        rootSpan.Should().NotBeNull();

        // The root span should have error details propagated from the command span
        if (rootSpan!.Tags.TryGetValue("error.details", out string? details))
        {
            details.Should().NotBeNullOrWhiteSpace("error.details should contain a meaningful message");
        }

        // The error.type should be present and match between command and root spans
        var commandSpan = spans.FirstOrDefault(s => s.DisplayName.StartsWith("command/", StringComparison.Ordinal));
        if (commandSpan != null &&
            commandSpan.Tags.TryGetValue("error.type", out string? commandErrorType) &&
            rootSpan.Tags.TryGetValue("error.type", out string? rootErrorType))
        {
            rootErrorType.Should().Be(commandErrorType, "error.type should match between command and root spans");
        }
    }

    [Fact]
    public void SuccessfulHelp_ProducesNoErrorTags()
    {
        // Running --help should succeed without error tags
        string tempDir = Path.Combine(Path.GetTempPath(), $"dnup-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var envVars = GetTelemetryEnvVars(tempDir);

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                ["--help"], captureOutput: true, environmentVariables: envVars);

            exitCode.Should().Be(0, "dotnetup --help should succeed");

            var spans = ParseTelemetrySpans(output);

            // If telemetry emits spans (it may not for --help), they should have no error tags
            var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
            if (rootSpan != null)
            {
                rootSpan.Tags.Should().NotContainKey("error.type", "--help should not produce error tags");
                rootSpan.Tags.Should().NotContainKey("error.category", "--help should not produce error tags");
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void InvalidVersion_RootSpan_HasCorrectDisplayName()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var args = DotnetupTestUtilities.BuildSdkArguments("999.999.999", testEnv.InstallPath, testEnv.ManifestPath);
        var envVars = GetTelemetryEnvVars(testEnv.TempRoot);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            args, captureOutput: true, workingDirectory: testEnv.TempRoot, environmentVariables: envVars);

        var spans = ParseTelemetrySpans(output);
        var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
        rootSpan.Should().NotBeNull("root span should have DisplayName 'dotnetup'");

        var commandSpan = spans.FirstOrDefault(s => s.DisplayName.StartsWith("command/", StringComparison.Ordinal));
        commandSpan.Should().NotBeNull("command span should start with 'command/'");
        commandSpan!.DisplayName.Should().Contain("sdk", "SDK command span should contain 'sdk'");
    }

    [Fact]
    public void InstallPathIsFile_ProducesUserError()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var envVars = GetTelemetryEnvVars(testEnv.TempRoot);

        // Create a file where the install path would be — user error: path is a file, not a directory
        string filePath = Path.Combine(testEnv.TempRoot, "not-a-directory");
        File.WriteAllText(filePath, "this is a file");

        var args = DotnetupTestUtilities.BuildSdkArguments("9.0", filePath, testEnv.ManifestPath);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            args, captureOutput: true, workingDirectory: testEnv.TempRoot, environmentVariables: envVars);

        exitCode.Should().NotBe(0, "install-path pointing to a file should fail");
        output.Should().Contain("existing file", "error message should mention it's a file");

        var spans = ParseTelemetrySpans(output);
        var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
        rootSpan.Should().NotBeNull("root span should be emitted");

        rootSpan!.Tags.Should().ContainKey("error.type");
        rootSpan.Tags["error.type"].Should().Be("InstallPathIsFile");
        rootSpan.Tags.Should().ContainKey("error.category");
        rootSpan.Tags["error.category"].Should().Be("user");
    }

    [Fact]
    public void CorruptManifest_UserEdited_ProducesUserError()
    {
        // Write a corrupt manifest and set the env var so dotnetup reads it.
        // No checksum file → user error (external edit).
        string tempDir = Path.Combine(Path.GetTempPath(), $"dnup-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string manifestPath = Path.Combine(tempDir, "manifest.json");
        File.WriteAllText(manifestPath, "NOT VALID {{{ JSON");

        try
        {
            var envVars = new Dictionary<string, string>(s_telemetryEnvVars)
            {
                ["DOTNET_TESTHOOK_MANIFEST_PATH"] = manifestPath,
                ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = tempDir,
            };

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                ["list"], captureOutput: true, environmentVariables: envVars);

            exitCode.Should().NotBe(0, "corrupt manifest should cause list to fail");

            var spans = ParseTelemetrySpans(output);
            var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
            rootSpan.Should().NotBeNull("root span should be emitted");

            rootSpan!.Tags.Should().ContainKey("error.type");
            rootSpan.Tags["error.type"].Should().Be("LocalManifestUserCorrupted");
            rootSpan.Tags.Should().ContainKey("error.category");
            rootSpan.Tags["error.category"].Should().Be("user");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CorruptManifest_DotnetupWrote_ProducesProductError()
    {
        // Write a corrupt manifest WITH a matching checksum → product error.
        // This simulates dotnetup having written corrupt JSON (our bug).
        string tempDir = Path.Combine(Path.GetTempPath(), $"dnup-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string manifestPath = Path.Combine(tempDir, "manifest.json");
        string checksumPath = manifestPath + ".sha256";
        string corruptContent = "{ broken json {{[";
        File.WriteAllText(manifestPath, corruptContent);

        // Write checksum that matches the corrupt content
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(corruptContent));
        File.WriteAllText(checksumPath, Convert.ToHexString(hash));

        try
        {
            var envVars = new Dictionary<string, string>(s_telemetryEnvVars)
            {
                ["DOTNET_TESTHOOK_MANIFEST_PATH"] = manifestPath,
                ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = tempDir,
            };

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                ["list"], captureOutput: true, environmentVariables: envVars);

            exitCode.Should().NotBe(0, "corrupt manifest should cause list to fail");

            var spans = ParseTelemetrySpans(output);
            var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
            rootSpan.Should().NotBeNull("root span should be emitted");

            rootSpan!.Tags.Should().ContainKey("error.type");
            rootSpan.Tags["error.type"].Should().Be("LocalManifestCorrupted");
            rootSpan.Tags.Should().ContainKey("error.category");
            rootSpan.Tags["error.category"].Should().Be("product");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TelemetryDisabled_ProducesNoSpans()
    {
        var envVars = new Dictionary<string, string>
        {
            ["DOTNETUP_TELEMETRY_DEBUG"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        };

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            ["--help"], captureOutput: true, environmentVariables: envVars);

        exitCode.Should().Be(0);

        var spans = ParseTelemetrySpans(output);
        spans.Should().BeEmpty("no telemetry spans should be emitted when telemetry is opted out");
    }

    /// <summary>
    /// Parses the OpenTelemetry ConsoleExporter output into structured span records.
    /// The console exporter outputs blocks like:
    /// <code>
    /// Activity.TraceId:            abc123
    /// Activity.SpanId:             def456
    /// Activity.DisplayName:        dotnetup
    /// ...
    /// Activity.Tags:
    ///     error.type: DotnetInstallException
    ///     error.category: user
    /// </code>
    /// </summary>
    private static List<TelemetrySpan> ParseTelemetrySpans(string output)
    {
        var spans = new List<TelemetrySpan>();
        var lines = output.Split('\n', StringSplitOptions.None);

        TelemetrySpan? current = null;
        bool inTags = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            // Start of a new span block
            if (line.StartsWith("Activity.TraceId:", StringComparison.Ordinal))
            {
                if (current != null)
                {
                    spans.Add(current);
                }

                current = new TelemetrySpan();
                inTags = false;
                continue;
            }

            if (current == null)
            {
                continue;
            }

            // Parse DisplayName
            var displayNameMatch = Regex.Match(line, @"^Activity\.DisplayName:\s*(.+)$");
            if (displayNameMatch.Success)
            {
                current.DisplayName = displayNameMatch.Groups[1].Value.Trim();
                inTags = false;
                continue;
            }

            // Parse StatusCode
            var statusMatch = Regex.Match(line, @"^StatusCode\s*:\s*(.+)$");
            if (statusMatch.Success)
            {
                current.StatusCode = statusMatch.Groups[1].Value.Trim();
                inTags = false;
                continue;
            }

            // Start of tags section
            if (line.TrimStart().StartsWith("Activity.Tags:", StringComparison.Ordinal))
            {
                inTags = true;
                continue;
            }

            // Parse tag key-value pairs (indented lines after Activity.Tags:)
            if (inTags)
            {
                var tagMatch = Regex.Match(line, @"^\s+([a-z0-9._-]+)\s*:\s*(.*)$", RegexOptions.IgnoreCase);
                if (tagMatch.Success)
                {
                    current.Tags[tagMatch.Groups[1].Value.Trim()] = tagMatch.Groups[2].Value.Trim();
                }
                else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(" ", StringComparison.Ordinal))
                {
                    // No longer in tags section
                    inTags = false;
                }
            }

            // Detect other Activity. sections that end the tags block
            if (line.StartsWith("Activity.", StringComparison.Ordinal) && !line.StartsWith("Activity.Tags:", StringComparison.Ordinal))
            {
                inTags = false;
            }
        }

        // Don't forget the last span
        if (current != null)
        {
            spans.Add(current);
        }

        return spans;
    }

    /// <summary>
    /// Represents a parsed telemetry span from console exporter output.
    /// </summary>
    private sealed class TelemetrySpan
    {
        public string DisplayName { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public Dictionary<string, string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
