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
        // Disable Azure Monitor exporters in tests. Without this, AzMon's HTTP
        // export and metric-extractor side-effects produce diagnostic stderr
        // writes and inject `_MS.*` tags onto LogRecords that interleave with
        // the console exporter output captured by the test, causing flaky
        // parse failures under parallel test execution.
        ["DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT"] = "1",
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
        rootSpan.Tags["error.category"].Should().Be("user", "requesting a nonexistent SDK version should be a user error");
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
        // The command completion record is emitted as `dotnetup/command` (DisplayName
        // == "command" after the parser strips the `dotnetup/` prefix). The
        // subcommand identity (e.g. "sdk/install") is carried in the `command.name`
        // attribute, not in the message.
        var commandSpan = spans.FirstOrDefault(s => s.DisplayName == "command");
        commandSpan.Should().NotBeNull("a `dotnetup/command` record should be emitted");

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
        var commandSpan = spans.FirstOrDefault(s => s.DisplayName == "command");
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
                ["--help"], captureOutput: true, workingDirectory: tempDir, environmentVariables: envVars);

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

        // The command completion record uses the stable message `dotnetup/command`
        // (DisplayName == "command" after `dotnetup/` is stripped). The subcommand
        // identity is carried in the `command.name` attribute (e.g. "sdk/install").
        var commandSpan = spans.FirstOrDefault(s => s.DisplayName == "command");
        commandSpan.Should().NotBeNull("a `dotnetup/command` completion record should be emitted");
        commandSpan!.Tags.Should().ContainKey("command.name", "command record should carry the subcommand in command.name");
        commandSpan.Tags["command.name"].Should().Contain("sdk", "SDK command should set command.name to a value containing 'sdk'");
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
            var envVars = GetTelemetryEnvVars(tempDir);
            envVars["DOTNET_TESTHOOK_MANIFEST_PATH"] = manifestPath;

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                ["list"], captureOutput: true, workingDirectory: tempDir, environmentVariables: envVars);

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
            var envVars = GetTelemetryEnvVars(tempDir);
            envVars["DOTNET_TESTHOOK_MANIFEST_PATH"] = manifestPath;

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                ["list"], captureOutput: true, workingDirectory: tempDir, environmentVariables: envVars);

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

    /// <summary>
    /// Regression coverage for Cat-3 throw conversion: paths that historically
    /// returned 1 without throwing (here: <c>UninstallWorkflow</c> when no
    /// matching install spec exists) must now throw <c>DotnetInstallException</c>
    /// so <c>CommandBase</c> calls <c>RecordException</c> and stamps
    /// <c>error.type</c> / <c>error.category</c> onto BOTH the root
    /// (<c>dotnetup/process/complete</c>) and command (<c>dotnetup/command</c>)
    /// LogRecords — those are the only telemetry surfaces the data-x ingestion
    /// pipeline reads.
    /// </summary>
    [Fact]
    public void Uninstall_NoMatchingTarget_StampsUserErrorTags_OnRootAndCommandLogs()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        // Empty test manifest — no installs to uninstall, so the workflow
        // throws DotnetInstallException(UninstallTargetNotFound).
        var args = DotnetupTestUtilities.BuildSdkUninstallArguments("9.0", testEnv.InstallPath, testEnv.ManifestPath);
        var envVars = GetTelemetryEnvVars(testEnv.TempRoot);

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            args, captureOutput: true, workingDirectory: testEnv.TempRoot, environmentVariables: envVars);

        exitCode.Should().NotBe(0, "uninstall against an empty manifest must fail");

        var spans = ParseTelemetrySpans(output);
        spans.Should().NotBeEmpty("console exporter should emit LogRecords");

        var rootSpan = spans.FirstOrDefault(s => s.DisplayName == "dotnetup");
        rootSpan.Should().NotBeNull("root LogRecord (dotnetup/process/complete) should be emitted");
        rootSpan!.Tags.Should().ContainKey("error.type", "Cat-3 throw must stamp error.type on root LogRecord");
        rootSpan.Tags["error.type"].Should().Be("UninstallTargetNotFound");
        rootSpan.Tags.Should().ContainKey("error.category");
        rootSpan.Tags["error.category"].Should().Be("user");

        var commandSpan = spans.FirstOrDefault(s => s.DisplayName == "command");
        commandSpan.Should().NotBeNull("command LogRecord (dotnetup/command) should be emitted");
        commandSpan!.Tags.Should().ContainKey("error.type", "Cat-3 throw must stamp error.type on command LogRecord");
        commandSpan.Tags["error.type"].Should().Be("UninstallTargetNotFound");
        commandSpan.Tags.Should().ContainKey("error.category");
        commandSpan.Tags["error.category"].Should().Be("user");
    }

    [Fact]
    public void TelemetryDisabled_ProducesNoSpans()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"dnup-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var envVars = new Dictionary<string, string>
            {
                ["DOTNETUP_TELEMETRY_DEBUG"] = "1",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = tempDir,
            };

            (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
                ["--help"], captureOutput: true, workingDirectory: tempDir, environmentVariables: envVars);

            exitCode.Should().Be(0);

            var spans = ParseTelemetrySpans(output);
            spans.Should().BeEmpty("no telemetry spans should be emitted when telemetry is opted out");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Parses the OpenTelemetry ConsoleExporter output for the
    /// <see cref="OpenTelemetry.Logs.OpenTelemetryLoggerOptions"/>'s log
    /// pipeline into structured records.
    /// </summary>
    /// <remarks>
    /// <para>
    /// We parse <c>LogRecord.*</c> blocks rather than <c>Activity.*</c> blocks
    /// because LogRecords are the only telemetry surface the data-x ingestion
    /// pipeline reads (it consumes the AppInsights <c>traces</c> table fed by
    /// <see cref="Microsoft.Extensions.Logging.ILogger"/>; it does NOT read
    /// the <c>dependencies</c> / <c>requests</c> tables fed by spans). Asserting on the
    /// LogRecord shape is therefore a faithful proxy for what the production
    /// AzMonitor log exporter will send.
    /// </para>
    /// <para>
    /// The console exporter prints blocks like:
    /// </para>
    /// <code>
    /// LogRecord.Timestamp:               2026-04-28T...
    /// LogRecord.TraceId:                 ...
    /// LogRecord.SpanId:                  ...
    /// LogRecord.CategoryName:            Microsoft.Dotnet.Bootstrapper
    /// LogRecord.Severity:                Info
    /// LogRecord.SeverityText:            Information
    /// LogRecord.FormattedMessage:        dotnetup/process/complete
    /// LogRecord.Body:                    dotnetup/process/complete
    /// LogRecord.Attributes (Key:Value):
    ///     exit.code: 1
    ///     command.status: error
    ///     error.type: LocalManifestCorrupted
    ///     error.category: product
    ///     operation.name: dotnetup/process/complete
    ///     operation.duration_ms: 339.5037
    ///
    /// Resource associated with LogRecord:
    ///     ...
    /// </code>
    /// <para>
    /// The <see cref="TelemetrySpan.DisplayName"/> field on the returned
    /// objects is the operation suffix after the leading <c>dotnetup/</c>:
    /// the root completion record becomes <c>"dotnetup"</c> (remapped from
    /// <c>process/complete</c>) and the command completion record is
    /// <c>"command"</c> — the subcommand identity (e.g. <c>sdk/install</c>) is
    /// carried in the <c>command.name</c> attribute, not in the message.
    /// </para>
    /// </remarks>
    private static List<TelemetrySpan> ParseTelemetrySpans(string output)
    {
        var records = new List<TelemetrySpan>();
        var lines = output.Split('\n', StringSplitOptions.None);

        TelemetrySpan? current = null;
        bool inAttributes = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            // Start of a new LogRecord block.
            if (line.StartsWith("LogRecord.Timestamp:", StringComparison.Ordinal))
            {
                if (current is { DisplayName.Length: > 0 })
                {
                    records.Add(current);
                }

                current = new TelemetrySpan();
                inAttributes = false;
                continue;
            }

            if (current == null)
            {
                continue;
            }

            // Parse FormattedMessage and convert to a "DisplayName"-shaped value.
            var formattedMatch = Regex.Match(line, @"^LogRecord\.FormattedMessage:\s*(.+)$");
            if (formattedMatch.Success)
            {
                string formatted = formattedMatch.Groups[1].Value.Trim();
                string trimmed = formatted.StartsWith("dotnetup/", StringComparison.Ordinal)
                    ? formatted["dotnetup/".Length..]
                    : formatted;

                // Remap the root completion record's display name from
                // `process/complete` back to `dotnetup` so existing tests
                // can keep using `DisplayName == "dotnetup"` as the root
                // filter. (Command records like `command/sdk` and the
                // explicit `error` record are unmodified.)
                current.DisplayName = string.Equals(trimmed, "process/complete", StringComparison.Ordinal)
                    ? "dotnetup"
                    : trimmed;
                inAttributes = false;
                continue;
            }

            // Parse Severity (Info / Error) into the StatusCode field for
            // backwards-compatibility with existing assertions.
            var severityMatch = Regex.Match(line, @"^LogRecord\.SeverityText:\s*(.+)$");
            if (severityMatch.Success)
            {
                current.StatusCode = severityMatch.Groups[1].Value.Trim();
                inAttributes = false;
                continue;
            }

            // Start of attributes section.
            if (line.StartsWith("LogRecord.Attributes", StringComparison.Ordinal))
            {
                inAttributes = true;
                continue;
            }

            // The attributes block ends at the blank line before
            // "Resource associated with LogRecord:" (or any other top-level
            // line).
            if (inAttributes)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    inAttributes = false;
                    continue;
                }

                var attrMatch = Regex.Match(line, @"^\s+([a-z0-9._-]+)\s*:\s*(.*)$", RegexOptions.IgnoreCase);
                if (attrMatch.Success)
                {
                    current.Tags[attrMatch.Groups[1].Value.Trim()] = attrMatch.Groups[2].Value.Trim();
                }
                else if (!line.StartsWith(" ", StringComparison.Ordinal))
                {
                    inAttributes = false;
                }
            }
        }

        if (current is { DisplayName.Length: > 0 })
        {
            records.Add(current);
        }

        return records;
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
