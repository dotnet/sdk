// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.Implementation;

/// <summary>
/// Application Insights context tag keys and Breeze schema field limits used when
/// serializing telemetry envelopes. Mirrors the constants used by
/// Azure.Monitor.OpenTelemetry.Exporter so the payloads we produce are byte-for-byte
/// compatible with what the Azure Monitor exporter would have sent.
/// </summary>
internal static class BreezeSchema
{
    // Context tag keys (envelope "tags" object).
    public const string OperationId = "ai.operation.id";
    public const string OperationParentId = "ai.operation.parentId";
    public const string OperationName = "ai.operation.name";
    public const string CloudRole = "ai.cloud.role";
    public const string CloudRoleInstance = "ai.cloud.roleInstance";
    public const string ApplicationVersion = "ai.application.ver";
    public const string InternalSdkVersion = "ai.internal.sdkVersion";

    // Envelope "name" discriminators.
    public const string RequestEnvelopeName = "Request";
    public const string DependencyEnvelopeName = "RemoteDependency";
    public const string MessageEnvelopeName = "Message";
    public const string ExceptionEnvelopeName = "Exception";

    // Data baseType discriminators.
    public const string RequestDataType = "RequestData";
    public const string RemoteDependencyDataType = "RemoteDependencyData";
    public const string MessageDataType = "MessageData";
    public const string ExceptionDataType = "ExceptionData";

    // The OpenTelemetry semantic-convention event name for exceptions.
    public const string ExceptionEventName = "exception";
    public const string ExceptionType = "exception.type";
    public const string ExceptionMessage = "exception.message";
    public const string ExceptionStacktrace = "exception.stacktrace";

    // Application Insights severity levels (MessageData/ExceptionData "severityLevel").
    // The Azure Monitor exporter serializes these as strings, not integers.
    public const string SeverityVerbose = "Verbose";
    public const string SeverityInformation = "Information";
    public const string SeverityWarning = "Warning";
    public const string SeverityError = "Error";
    public const string SeverityCritical = "Critical";

    // Property keys the Azure Monitor log exporter stamps onto every log envelope.
    public const string CategoryNameProperty = "CategoryName";
    public const string EventIdProperty = "EventId";
    public const string EventNameProperty = "EventName";
    public const string OriginalFormatKey = "{OriginalFormat}";
    public const string OriginalFormatProperty = "OriginalFormat";

    // Breeze schema data version for baseData payloads.
    public const int DataVersion = 2;

    // Field length limits (mirrors Azure exporter SchemaConstants).
    public const int KvpMaxKeyLength = 150;
    public const int KvpMaxValueLength = 8192;
    public const int NameMaxLength = 1024;
    public const int OperationNameMaxLength = 1024;
    public const int CloudRoleMaxLength = 256;
    public const int CloudRoleInstanceMaxLength = 256;
    public const int ApplicationVersionMaxLength = 1024;
    public const int SdkVersionMaxLength = 64;
    public const int MessageMaxLength = 32768;
    public const int ResultCodeMaxLength = 1024;
    public const int ExceptionMessageMaxLength = 32768;
    public const int ExceptionStackMaxLength = 32768;
    public const int ExceptionTypeNameMaxLength = 1024;

    public static string? Truncate(string? value, int maxLength)
        => value is not null && value.Length > maxLength ? value.Substring(0, maxLength) : value;
}
