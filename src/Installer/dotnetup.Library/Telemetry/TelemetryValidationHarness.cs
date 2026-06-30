// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// TEMPORARY telemetry-delivery validation harness (revert before merge).
/// Gated entirely behind <see cref="Constants.Telemetry.SimulateErrorEnvVar"/>:
/// when the env var is unset (the normal case) this is a no-op. When set, the
/// next command throws a deterministic exception that maps to an EXISTING,
/// already-classified <c>error.type</c> value, so no new telemetry events are
/// emitted (which would need 3-day GDPR re-classification). This lets us verify
/// which (flush budget × environment) combinations actually reach the dashboard.
/// </summary>
internal static class TelemetryValidationHarness
{
    /// <summary>
    /// Throws a simulated, pre-classified exception when the simulate-error env
    /// var is set. Called from <see cref="CommandBase.Execute"/> before the
    /// command body so the throw flows through the real RecordException pipeline
    /// (failing command row + propagated root row), exactly like an organic
    /// failure. Each code maps to an error.type that already exists in the
    /// dashboard's classification catalog.
    /// </summary>
    public static void ThrowIfRequested()
    {
        var code = Environment.GetEnvironmentVariable(Constants.Telemetry.SimulateErrorEnvVar);
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        throw code.Trim().ToLowerInvariant() switch
        {
            // -- product-category error.type values --
            "1" or "product" or "invalidoperation" =>
                new InvalidOperationException(
                    "Simulated product error -> error.type=InvalidOperation (telemetry validation harness)."),
            "manifest" or "localmanifestcorrupted" =>
                new DotnetInstallException(
                    DotnetInstallErrorCode.LocalManifestCorrupted,
                    "Simulated -> error.type=LocalManifestCorrupted (telemetry validation harness)."),
            "signature" or "signatureverificationfailed" =>
                new DotnetInstallException(
                    DotnetInstallErrorCode.SignatureVerificationFailed,
                    "Simulated -> error.type=SignatureVerificationFailed (telemetry validation harness)."),

            // -- user-category error.type values --
            "platform" or "platformnotsupported" =>
                new DotnetInstallException(
                    DotnetInstallErrorCode.PlatformNotSupported,
                    "Simulated -> error.type=PlatformNotSupported (telemetry validation harness)."),
            "permission" or "permissiondenied" =>
                new UnauthorizedAccessException(
                    "Simulated -> error.type=PermissionDenied (telemetry validation harness)."),

            // default: product InvalidOperation
            _ => new InvalidOperationException(
                $"Simulated product error '{code}' -> error.type=InvalidOperation (telemetry validation harness)."),
        };
    }
}
