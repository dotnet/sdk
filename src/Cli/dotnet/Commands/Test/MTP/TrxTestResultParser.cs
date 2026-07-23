// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Xml.Linq;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Parses a Microsoft.Testing.Platform TRX report (Microsoft.Testing.Extensions.TrxReport) into a
/// minimal model. Used for standalone (wasm) test hosts that cannot stream results back over the
/// named pipe: the host writes an on-disk TRX, which the SDK reads to drive the terminal reporter.
/// </summary>
internal static class TrxTestResultParser
{
    private static readonly XNamespace s_ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Reads and parses the TRX at <paramref name="filePath"/>. Returns <see langword="null"/> when
    /// the file is missing or cannot be parsed (e.g. a crashed host that never wrote a full TRX).
    /// </summary>
    public static TrxReport? TryParse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            XDocument document;
            using (var stream = File.OpenRead(filePath))
            {
                document = XDocument.Load(stream);
            }

            var results = new List<TrxTestResult>();
            foreach (var element in document.Descendants(s_ns + "UnitTestResult"))
            {
                results.Add(ParseResult(element));
            }

            string? runOutcome = document.Descendants(s_ns + "ResultSummary").FirstOrDefault()?.Attribute("outcome")?.Value;

            return new TrxReport(results, runOutcome);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static TrxTestResult ParseResult(XElement element)
    {
        string displayName = element.Attribute("testName")?.Value ?? string.Empty;
        string outcome = element.Attribute("outcome")?.Value ?? string.Empty;
        // testId is a stable GUID; fall back to the display name so the reporter always has a uid.
        string uid = element.Attribute("testId")?.Value is { Length: > 0 } testId ? testId : displayName;

        TimeSpan? duration = null;
        if (element.Attribute("duration")?.Value is { Length: > 0 } durationValue &&
            TimeSpan.TryParse(durationValue, CultureInfo.InvariantCulture, out var parsedDuration))
        {
            duration = parsedDuration;
        }

        XElement? output = element.Element(s_ns + "Output");
        string? standardOutput = output?.Element(s_ns + "StdOut")?.Value;
        string? errorOutput = output?.Element(s_ns + "StdErr")?.Value;

        XElement? errorInfo = output?.Element(s_ns + "ErrorInfo");
        string? errorMessage = errorInfo?.Element(s_ns + "Message")?.Value;
        string? stackTrace = errorInfo?.Element(s_ns + "StackTrace")?.Value;

        return new TrxTestResult(uid, displayName, outcome, duration, standardOutput, errorOutput, errorMessage, stackTrace);
    }
}

internal sealed record TrxReport(IReadOnlyList<TrxTestResult> Results, string? RunOutcome);

internal sealed record TrxTestResult(
    string Uid,
    string DisplayName,
    string Outcome,
    TimeSpan? Duration,
    string? StandardOutput,
    string? ErrorOutput,
    string? ErrorMessage,
    string? StackTrace);
