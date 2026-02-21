// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Constants for telemetry tag names used across dotnetup.
/// Centralizes tag names to prevent typos and enable rename-safe refactoring.
/// </summary>
internal static class TelemetryTagNames
{
    // Command-level tags
    public const string CommandName = "command.name";
    public const string ExitCode = "exit.code";
    public const string SessionId = "session.id";
    public const string Caller = "caller";

    // Error tags
    public const string ErrorType = "error.type";
    public const string ErrorCategory = "error.category";
    public const string ErrorCode = "error.code";
    public const string ErrorMessage = "error.message";
    public const string ErrorDetails = "error.details";
    public const string ErrorHttpStatus = "error.http_status";
    public const string ErrorHResult = "error.hresult";
    public const string ErrorSourceLocation = "error.source_location";
    public const string ErrorExceptionChain = "error.exception_chain";

    // Install tags
    public const string InstallComponent = "install.component";
    public const string InstallRequestedVersion = "install.requested_version";
    public const string InstallPathExplicit = "install.path_explicit";
    public const string InstallPathType = "install.path_type";
    public const string InstallPathSource = "install.path_source";
    public const string InstallHasGlobalJson = "install.has_global_json";
    public const string InstallExistingInstallType = "install.existing_install_type";
    public const string InstallSetDefault = "install.set_default";
    public const string InstallResolvedVersion = "install.resolved_version";
    public const string InstallResult = "install.result";
    public const string InstallMutexLockFailed = "install.mutex_lock_failed";
    public const string InstallMutexLockPhase = "install.mutex_lock_phase";
    public const string InstallMigratingFromAdmin = "install.migrating_from_admin";
    public const string InstallAdminVersionCopied = "install.admin_version_copied";

    // Dotnet request tags
    public const string DotnetRequestSource = "dotnet.request_source";
    public const string DotnetRequested = "dotnet.requested";
    public const string DotnetRequestedVersion = "dotnet.requested_version";
}
