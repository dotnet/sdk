# Responsibly managing telemetry in external components

Many components are _delivered_ with or by the .NET SDK but want to collect and
manage telemetry. The SDK has telemetry collection mechanisms that may appear
attractive, but present down-sides for these authors.

This document clarifies some guidelines for authors of components that are consumed by the .NET SDK but want to own their own telemetry.

## Sending telemetry

### DO create and manage your own MSBuild Task for sending telemetry

This allows you to have full control over the telemetry you send, where it is
sent, and any PII masking requirements that are unique to your product.
Attempting to use the .NET SDK's `AllowEmptyTelemetry` mechanism is not
recommended for most internal partners and all external users. This is because
`AllowEmptyTelemetry`
  * is allow-listed for known events only, so your telemetry will not be sent
  * sends to the SDK's telemetry storage, which your team may not have access to
  * is dependent on the SDK version the user uses, which may lag behind the latest available

### DO NOT use the MSBuild Engine telemetry APIs for logging telemetry

These APIs, while convenient, require the MSBuild Engine Host (`dotnet build`,
`msbuild.exe`, Visual Studio Project system) to have configured a telemetry
collector. This is not guaranteed to be the case for all users of your component,
and the collector configured may not send telemetry in the manner you expect, or
to destinations you expect.

## Managing telemetry

### DO adhere to the SDK telemetry opt-out

The SDK has an [opt out](https://learn.microsoft.com/dotnet/core/tools/telemetry#how-to-opt-out) mechanism for telemetry that all SDK-generated
telemetry should adhere to. When running in the context of the SDK that means your
telemetry should adhere to this signal as well. This opt-out mechanism is an
environment variable, but the default value of this variable changes for
Microsoft-authored and source-built SDKs. The SDK should provide a unified
mechanism for tooling authors to rely on instead of probing for this value.
