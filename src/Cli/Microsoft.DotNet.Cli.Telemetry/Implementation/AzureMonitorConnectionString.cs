// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// A minimal parser for the Application Insights connection string. We only need the
/// instrumentation key (stamped into every telemetry envelope's <c>iKey</c>) and the
/// ingestion endpoint (the base of the Breeze <c>/v2.1/track</c> upload URL).
/// </summary>
internal sealed record AzureMonitorConnectionString(string InstrumentationKey, Uri IngestionEndpoint)
{
    private const string InstrumentationKeyName = "InstrumentationKey";
    private const string IngestionEndpointName = "IngestionEndpoint";
    private const string DefaultIngestionEndpoint = "https://dc.services.visualstudio.com/";

    /// <summary>
    /// The full URL telemetry is POSTed to, e.g.
    /// <c>https://&lt;region&gt;.in.applicationinsights.azure.com/v2.1/track</c>.
    /// </summary>
    public Uri TrackUri { get; } = new Uri(IngestionEndpoint, "v2.1/track");

    /// <summary>
    /// Parses <paramref name="connectionString"/> into its instrumentation key and ingestion
    /// endpoint. Returns <see langword="null"/> when the string is missing an instrumentation
    /// key, which is the only field without a safe default.
    /// </summary>
    public static AzureMonitorConnectionString? Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        string? instrumentationKey = null;
        var ingestionEndpoint = DefaultIngestionEndpoint;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = part.Substring(0, separatorIndex).Trim();
            var value = part.Substring(separatorIndex + 1).Trim();

            if (key.Equals(InstrumentationKeyName, StringComparison.OrdinalIgnoreCase))
            {
                instrumentationKey = value;
            }
            else if (key.Equals(IngestionEndpointName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
            {
                ingestionEndpoint = value;
            }
        }

        if (string.IsNullOrWhiteSpace(instrumentationKey))
        {
            return null;
        }

        if (!Uri.TryCreate(EnsureTrailingSlash(ingestionEndpoint), UriKind.Absolute, out var endpointUri))
        {
            return null;
        }

        return new AzureMonitorConnectionString(instrumentationKey, endpointUri);
    }

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith('/') ? value : value + "/";
}
