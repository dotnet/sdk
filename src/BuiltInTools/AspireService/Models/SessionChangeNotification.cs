// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Microsoft.WebTools.AspireServer.Models;

internal static class NotificationType
{
    public const string ProcessRestarted = "processRestarted";
    public const string SessionTerminated = "sessionTerminated";
    public const string ServiceLogs = "serviceLogs";
}

internal class SessionNotificationBase
{
    public const string Url = "/notify";

    [Required]
    [JsonPropertyName("notification_type")]
    public string NotificationType { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;
}

internal class SessionChangeNotification : SessionNotificationBase
{
    [JsonPropertyName("pid")]
    public int PID { get; set; }

    [JsonPropertyName("exit_code")]
    public int? ExitCode { get; set; }
}

internal class SessionLogsNotification : SessionNotificationBase
{
    [JsonPropertyName("is_std_err")]
    public bool IsStdErr { get; set; }

    [JsonPropertyName("log_message")]
    public string LogMessage { get; set; } = string.Empty;
}
