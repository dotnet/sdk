# NuGet Package Signature Verification

## Overview

`NuGetPackageDownloader` can verify NuGet package (`.nupkg`) signatures after download using
`FirstPartyNuGetPackageSigningVerifier`. This applies to any caller — workload commands,
`dotnet tool install`, etc.

Verification is controlled by the `verifySignatures` constructor parameter and an additional
platform gate applied in the constructor.

## NuGet Signature Types

NuGet packages can carry two kinds of signatures:
- **Repository signature** — applied automatically by nuget.org. Proves the package was
  published through nuget.org, but says nothing about the original author.
- **Author signature** — applied by the package publisher. Many packages (especially from
  individuals) are not author-signed.

## Platform Gate

Even when the caller passes `verifySignatures: true`, the constructor may disable verification
based on the OS and the `DOTNET_NUGET_SIGNATURE_VERIFICATION` environment variable:

| Platform | Env var unset | `=true` | `=false` |
|----------|--------------|---------|----------|
| Windows  | verify | verify | verify |
| Linux    | **verify** | verify | skip |
| macOS    | **skip** | verify | skip |

- **Windows**: always enabled (uses the OS certificate store).
- **Linux**: enabled by default. NuGet uses root certificate bundles (`.pem` files) from the
  Microsoft Trusted Root Program (TRP), shipped as point-in-time snapshots in the SDK. These
  bundles are only updated by installing a newer SDK, so a certificate chaining to a
  newly-added root can fail verification until the bundle catches up.
- **macOS**: disabled by default. macOS does not fully support the certificate store NuGet uses
  for verification, causing spurious failures
  ([#46857](https://github.com/dotnet/sdk/issues/46857)).

When verification is downgraded by this gate, a diagnostic message is logged via `ILogger`.

> **Note:** `NuGetSignatureVerificationEnabler` is a separate, unrelated system. It sets
> `DOTNET_NUGET_SIGNATURE_VERIFICATION` for *forwarded* NuGet/MSBuild commands (e.g.,
> `dotnet restore`). It does **not** affect the in-process `NuGetPackageDownloader`.

## Verification Modes

`NuGetPackageDownloader.VerifySigning()` runs after every download when `_verifySignatures`
is `true`. It supports two modes selected by `_shouldUsePackageSourceMapping`:

| Mode | Method | What it checks | When used |
|------|--------|---------------|-----------|
| **Strict** | `Verify()` = `NuGetVerify()` + `IsFirstParty()` | Valid signature AND Microsoft author cert | Source mapping NOT used |
| **Relaxed** | `NuGetVerify()` only | Valid signature from any trusted signer | Source mapping IS used |

- `NuGetVerify()` shells out to `dotnet nuget verify --all`, which finds the SDK's TRP
  certificate bundles automatically.
- `IsFirstParty()` uses the NuGet.Packaging API to extract the signing certificate chain and
  compares the author certificate thumbprint against known Microsoft values
  (`_firstPartyCertificateThumbprints` / `_upperFirstPartyCertificateThumbprints`).

### Repository must require signing

Verification only runs when the NuGet repository reports `AllRepositorySigned == true`.
If the repository does not advertise signing, packages are accepted without checks.

## Factory Method: `CreateForWorkloads()`

Workload code uses `CreateForWorkloads()` instead of the constructor directly to ensure
consistent defaults:
- Always creates a `FirstPartyNuGetPackageSigningVerifier`
- `shouldUsePackageSourceMapping = true`
- `NullLogger` and `null` reporter unless overridden

## Key Files

- `NuGetPackageDownloader.cs` — constructor (platform gate), `VerifySigning()`, `CreateForWorkloads()`
- `FirstPartyNuGetPackageSigningVerifier.cs` — `Verify()`, `NuGetVerify()`, `IsFirstParty()`
- `IFirstPartyNuGetPackageSigningVerifier.cs` — interface
- `NuGetSignatureVerificationEnabler.cs` — env var enabler for forwarded commands (unrelated)
