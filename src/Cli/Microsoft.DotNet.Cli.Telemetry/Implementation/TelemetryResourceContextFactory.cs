// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Builds the <see cref="TelemetryResourceContext"/> (cloud role / role instance / application
/// version / SDK version) from an OpenTelemetry <see cref="Resource"/>. Shared by the trace and
/// log exporters so both signals stamp identical Application Insights context tags.
/// </summary>
internal static class TelemetryResourceContextFactory
{
    // OpenTelemetry resource attribute keys (semantic conventions).
    private const string ServiceNameKey = "service.name";
    private const string ServiceNamespaceKey = "service.namespace";
    private const string ServiceInstanceIdKey = "service.instance.id";
    private const string ServiceVersionKey = "service.version";

    public static TelemetryResourceContext FromResource(Resource? resource)
    {
        string? serviceName = null;
        string? serviceNamespace = null;
        string? roleInstance = null;
        string? applicationVersion = null;

        if (resource is not null)
        {
            foreach (var attribute in resource.Attributes)
            {
                switch (attribute.Key)
                {
                    case ServiceNameKey:
                        serviceName = attribute.Value?.ToString();
                        break;
                    case ServiceNamespaceKey:
                        serviceNamespace = attribute.Value?.ToString();
                        break;
                    case ServiceInstanceIdKey:
                        roleInstance = attribute.Value?.ToString();
                        break;
                    case ServiceVersionKey:
                        applicationVersion = attribute.Value?.ToString();
                        break;
                }
            }
        }

        var roleName = (serviceNamespace, serviceName) switch
        {
            (not null, not null) => $"{serviceNamespace}.{serviceName}",
            (null, not null) => serviceName,
            _ => serviceNamespace,
        };

        return new TelemetryResourceContext(roleName, roleInstance, applicationVersion, SdkVersion.Value);
    }

    /// <summary>
    /// Replicates the <c>ai.internal.sdkVersion</c> tag produced by the Azure Monitor
    /// exporter (<c>dotnet&lt;runtime&gt;:otel&lt;otel&gt;:ext&lt;exporter&gt;</c>) so that
    /// dashboards which filter on it continue to work. The Azure Monitor exporter version is
    /// supplied by the build as assembly metadata (see the <c>AssemblyMetadata</c> item in the
    /// project file) so this type does not depend on the exporter assembly directly.
    /// </summary>
    private static readonly Lazy<string> SdkVersion = new(() =>
    {
        var dotnet = GetAssemblyVersion(typeof(object));
        var otel = GetAssemblyVersion(typeof(Sdk));
        var ext = NormalizeVersion(GetExporterVersion());
        return $"dotnet{dotnet}:otel{otel}:ext{ext}";
    });

    // Key of the assembly metadata entry carrying the Azure.Monitor.OpenTelemetry.Exporter
    // package version. Must match the AssemblyMetadata item defined in the project file.
    private const string ExporterVersionMetadataKey = "AzureMonitorOpenTelemetryExporterVersion";

    private static string? GetExporterVersion() =>
        typeof(TelemetryResourceContextFactory).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, ExporterVersionMetadataKey, StringComparison.Ordinal))?
            .Value;

    private static string GetAssemblyVersion(Type type) =>
        NormalizeVersion(type.Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion);

    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return "0.0.0";
        }

        // Strip build metadata / source-revision suffixes, matching the Azure exporter.
        var separatorIndex = version.IndexOfAny(['+', '@', ' ']);
        var normalized = separatorIndex >= 0 ? version.Substring(0, separatorIndex) : version;
        return normalized.Length > 20 ? normalized.Substring(0, 20) : normalized;
    }
}
