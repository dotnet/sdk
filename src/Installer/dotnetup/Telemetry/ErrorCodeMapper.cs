// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Categorizes errors for telemetry purposes.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Product errors - issues we can take action on (bugs, crashes, server issues).
    /// These count against product quality metrics.
    /// </summary>
    Product,

    /// <summary>
    /// User errors - issues caused by user input or environment we can't control.
    /// Examples: invalid version, disk full, network timeout, permission denied.
    /// These are tracked separately and don't count against primary success rate metrics.
    /// </summary>
    User
}

/// <summary>
/// Error info extracted from an exception for telemetry.
/// </summary>
/// <param name="ErrorType">The error type/code for telemetry.</param>
/// <param name="Category">Whether this is a product or user error.</param>
/// <param name="StatusCode">HTTP status code if applicable.</param>
/// <param name="HResult">Win32 HResult if applicable.</param>
/// <param name="Details">Additional context (no PII - sanitized values only).</param>
/// <param name="SourceLocation">Method name from our code where error occurred (no file paths).</param>
/// <param name="ExceptionChain">Chain of exception types for wrapped exceptions.</param>
public sealed record ExceptionErrorInfo(
    string ErrorType,
    ErrorCategory Category = ErrorCategory.Product,
    int? StatusCode = null,
    int? HResult = null,
    string? Details = null,
    string? SourceLocation = null,
    string? ExceptionChain = null);

/// <summary>
/// Public façade for error telemetry: maps exceptions to <see cref="ExceptionErrorInfo"/>
/// and applies error tags to OpenTelemetry activities.
/// </summary>
/// <remarks>
/// Implementation is split across single-responsibility helpers:
/// <list type="bullet">
///   <item><see cref="ExceptionErrorMapper"/> — exception-type dispatch and enrichment</item>
///   <item><see cref="ErrorCategoryClassifier"/> — Product vs User classification + HResult mapping</item>
///   <item><see cref="NetworkErrorAnalyzer"/> — PII-safe network exception diagnostics</item>
///   <item><see cref="ExceptionInspector"/> — stack-trace source location and exception chains</item>
/// </list>
/// </remarks>
public static class ErrorCodeMapper
{
    /// <summary>
    /// Applies error info tags to an activity. This centralizes the tag-setting logic
    /// to avoid code duplication across progress targets and telemetry classes.
    /// </summary>
    /// <param name="activity">The activity to tag (can be null).</param>
    /// <param name="errorInfo">The error info to apply.</param>
    /// <param name="errorCode">Optional error code override.</param>
    public static void ApplyErrorTags(Activity? activity, ExceptionErrorInfo errorInfo, string? errorCode = null)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, errorInfo.ErrorType);
        activity.SetTag(TelemetryTagNames.ErrorType, errorInfo.ErrorType);
        if (errorCode is not null)
        {
            activity.SetTag(TelemetryTagNames.ErrorCode, errorCode);
        }
        activity.SetTag(TelemetryTagNames.ErrorCategory, errorInfo.Category.ToString().ToLowerInvariant());

        // Use pattern matching to set optional tags only if they have values
        if (errorInfo is { StatusCode: { } statusCode })
            activity.SetTag(TelemetryTagNames.ErrorHttpStatus, statusCode);
        if (errorInfo is { HResult: { } hResult })
            activity.SetTag(TelemetryTagNames.ErrorHResult, hResult);
        if (errorInfo is { Details: { } details })
            activity.SetTag(TelemetryTagNames.ErrorDetails, details);
        if (errorInfo is { SourceLocation: { } sourceLocation })
            activity.SetTag(TelemetryTagNames.ErrorSourceLocation, sourceLocation);
        if (errorInfo is { ExceptionChain: { } exceptionChain })
            activity.SetTag(TelemetryTagNames.ErrorExceptionChain, exceptionChain);

        // NOTE: We intentionally do NOT call activity.RecordException(ex)
        // because exception messages/stacks can contain PII
    }

    /// <summary>
    /// Extracts error info from an exception.
    /// </summary>
    /// <param name="ex">The exception to analyze.</param>
    /// <returns>Error info with type name and contextual details.</returns>
    public static ExceptionErrorInfo GetErrorInfo(Exception ex) => ExceptionErrorMapper.Map(ex);
}
