# dotnetup Signature Verification

This document describes the detached CMS / PKCS#7 signatures (`.p7s`)
that dotnetup accepts when verifying .NET release artifacts (manifest
JSON files and release archives such as `.zip` / `.tar.gz`). It states
the checks the verifier performs, in order, and the `FailureCode` each
one emits on rejection. The intent is descriptive — it explains what the
in-tree verifier does and why — but the checks themselves are normative
for any signature dotnetup will accept: a signature that fails any
**MUST** below is rejected.

The verifier is implemented by the internal `SignatureVerifier` class in
the `Microsoft.Dotnet.Installation` library. It is cross-platform (Windows,
Linux, macOS) and uses only the BCL (`System.Security.Cryptography.Pkcs`,
`System.Security.Cryptography.X509Certificates`, `System.Text.Json`).

The only content-specific check
(§9, JSON expiration) is opt-in via
`SignatureVerificationOptions.RequireJsonExpirationField`.

Although the verifier lives in `Microsoft.Dotnet.Installation` and is
named for dotnetup, the acceptance rules below apply to any tool that
consumes a `.p7s` covering a .NET release artifact — for example the
install scripts (`dotnet-install.sh` / `dotnet-install.ps1`) or other
in-house installers. Such consumers may layer additional concerns on
top (e.g. offline-revocation support for air-gapped scenarios), but a
signature that fails any **MUST** below is not a valid .NET release
signature and should be rejected regardless of the consumer.

> **TODO:** Manifest verification covers archives transitively: the
> signed manifest pins each archive's SHA-512, and the archive download
> path validates that hash before extraction, so re-verifying every
> `.zip` / `.tar.gz` against its own `.p7s` would be redundant and slow
> (multi-hundred-MB CMS verifications on every install).
>
> Direct archive verification applies to cases where dotnetup downloads
> an archive **without** an accompanying signed manifest — e.g. daily /
> preview builds. Once those builds publish detached signatures, wiring
> their download paths to call `SignatureVerifier.Verify` with
> `RequireJsonExpirationField = false` is a follow-up.

## 1. Inputs

Callers pass:

| Parameter   | Meaning                                                                          |
| ----------- | -------------------------------------------------------------------------------- |
| `content`   | `byte[]` — the signed bytes (manifest JSON or archive contents).                 |
| `signature` | `byte[]` — the detached CMS `.p7s` blob covering `content`.                      |
| `options`   | `SignatureVerificationOptions` — trust anchors and policy knobs (below).         |

`SignatureVerificationOptions` carries:

| Field                          | Meaning                                                                                                                |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| `TrustedCodeSigningRoots`      | `X509Certificate2Collection` of roots trusted to chain code-signing certificates.                                       |
| `TrustedTimestampRoots`        | `X509Certificate2Collection` of roots trusted for RFC 3161 TSAs.                                                        |
| `RevocationMode`               | `Online` (default), `Offline`, or `NoCheck`. See §6.                                                                   |
| `RequireJsonExpirationField`   | `bool` (default `true`). Gates §9. Archive callers should set `false`.                                                 |
| `MaxAcceptableSigningAge`      | **Reserved.** Time-bounded trust window for non-`Online` revocation modes. Declared but not enforced.                  |

The `Microsoft.Dotnet.Installation` library bundles `codesignctl.pem` and
`timestampctl.pem` (sourced from `src/Layout/redist/trustedroots/`) and
loads them as the default trust anchors via `DefaultSignatureOptions`. The
PEMs are Certificate Trust Lists — concatenations of PEM-encoded X.509
certificates.

When either trusted-root collection is empty, the verifier emits
`TrustedRootsEmpty`. Without that check, a misconfigured caller (e.g. a bad
PEM resource) would silently fall back to an OS-only chain build and accept
things it shouldn't.

> **TODO:** `MaxAcceptableSigningAge` is reserved for an air-gap follow-up.
> The field is declared on `SignatureVerificationOptions` but the verifier
> does not consult it. Implement enforcement before relying on
> `RevocationMode.Offline` or `NoCheck` in production.

## 2. Container format

- The signature MUST be a PKCS#7 / CMS `SignedData` structure (RFC 5652).
- It MUST be **detached**: the encapsulated content is absent and the
  verifier supplies `content` itself.
- The encapsulated content type OID MUST be `1.2.840.113549.1.7.1`
  (`id-data`).
- The blob MUST carry exactly one `SignerInfo`.
- The signer certificate MUST be embedded in the CMS `certificates` bag
  and reachable from the signer identifier.

Failures: `SigDecodeFailed`, `SigNotCms`, `SigMultipleSigners`, `SignerCertMissing`.

## 3. Cryptographic integrity

The verifier calls `SignedCms.CheckSignature(verifySignatureOnly: true)`.
Trust is evaluated separately in §6, because the verifier enforces a
custom trust store (§5) that cannot be expressed via `CheckSignature`'s
built-in chain build.

Failure: `SigCryptoInvalid`.

## 4. Algorithm policy

- Digest algorithm MUST be one of: SHA-256, SHA-384, SHA-512. SHA-1 and
  MD5 are rejected.
- Signer public-key algorithm MUST be RSA (`1.2.840.113549.1.1.1`) or
  ECDSA (`1.2.840.10045.2.1`). DSA and other algorithms are rejected.

Failures: `WeakDigest`, `WeakSignatureAlgorithm`.

## 5. Signer certificate policy

### 5.1 Subject

The signer's Distinguished Name MUST consist of **exactly** the following
RDNs (order-insensitive, OID-level comparison; multi-valued RDNs are
rejected):

| OID        | Short | Value                  |
| ---------- | ----- | ---------------------- |
| `2.5.4.3`  | `CN`  | `Microsoft Corporation`|
| `2.5.4.11` | `OU`  | `.NET Release`         |
| `2.5.4.10` | `O`   | `Microsoft Corporation`|
| `2.5.4.7`  | `L`   | `Redmond`              |
| `2.5.4.8`  | `S`   | `Washington`           |
| `2.5.4.6`  | `C`   | `US`                   |

Failure: `SubjectMismatch`.

### 5.2 Issuer

The signer certificate's **immediate issuer** Distinguished Name MUST
consist of exactly the following RDNs (same comparison rules as §5.1).
This pins the signer to a specific DigiCert code-signing intermediate as
a defense-in-depth check on top of §6's chain build:

| OID        | Short | Value                                                       |
| ---------- | ----- | ----------------------------------------------------------- |
| `2.5.4.3`  | `CN`  | `DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1`  |
| `2.5.4.10` | `O`   | `DigiCert, Inc.`                                            |
| `2.5.4.6`  | `C`   | `US`                                                        |

Failure: `IssuerMismatch`.

### 5.3 Extended Key Usage

- The signer certificate MUST carry an EKU extension.
- The EKU MUST contain **exactly one** OID: `1.3.6.1.5.5.7.3.3`
  (`id-kp-codeSigning`).
- The EKU MUST NOT contain `2.5.29.37.0` (`anyExtendedKeyUsage`).

Failures: `EkuMissing`, `EkuNotExclusiveCodeSign`.

## 6. Certificate chain

The verifier builds an `X509Chain` for the signer certificate with:

- `TrustMode = CustomRootTrust`.
- `CustomTrustStore` = the union of `options.TrustedCodeSigningRoots` and
  the OS root store (`StoreName.Root`, both `CurrentUser` and
  `LocalMachine` where available).
- `ExtraStore` = the CMS certificate bag from §2. Intermediate
  certificates for both the primary signature and the timestamp signature
  (§7) MUST be embedded in their respective CMS certificate bags; the
  verifier does not follow AIA URLs.
- `RevocationMode` is configurable via `options.RevocationMode`:
  - `Online` (default, recommended): CRL/OCSP MUST succeed; fail closed
    when unreachable.
  - `Offline`: use locally cached CRLs only.
  - `NoCheck`: skip revocation entirely. Should be paired with
    `MaxAcceptableSigningAge` (see §1 TODO).
- `RevocationFlag = EntireChain`, `UrlRetrievalTimeout = 30s`.
- `ApplicationPolicy` MUST contain `1.3.6.1.5.5.7.3.3`
  (`id-kp-codeSigning`) so the chain engine rejects mis-purposed
  intermediates and cross-signed paths whose constraints would forbid
  code signing. This is defense-in-depth on top of the post-hoc EKU check
  in §5.3.
- `VerificationTime` = the authoritative RFC 3161 TSA timestamp from §7
  (or current UTC when no TSA timestamp is available).
- `VerificationFlags = NoFlag`. The verifier MUST NOT set
  `IgnoreNotTimeValid`. Release artifacts are intended-fresh: an expired
  signer certificate at time of consumption means the artifact is stale
  even if the signature was valid at issuance. NuGet's package
  verification path ignores `NotTimeValid` because packages are immutable
  historical artifacts; release artifacts are not.

On Windows, when chain build fails and any chain-status flag includes
`UntrustedRoot`, the verifier retries up to 3 times with a 1 s sleep
between attempts. This mirrors NuGet's
[`RetriableX509ChainBuildPolicy`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/Signing/ChainBuilding/RetriableX509ChainBuildPolicy.cs)
and defends against a documented Windows transient where the OS root
store briefly reports `UntrustedRoot` under load. Non-Windows builds skip
the retry, matching NuGet's `X509ChainBuildPolicyFactory`.

After chain evaluation the verifier disposes every
`X509ChainElement.Certificate` to avoid finalizer pressure on the OS
handle (mirrors NuGet's `X509ChainHolder.Dispose`).

The chain MUST build with zero `ChainStatus` entries. Status flags map
to:

- `Revoked` → `Revoked`.
- `OfflineRevocation` or `RevocationStatusUnknown` →
  `RevocationUnavailable`. Offline revocation is not accepted under
  `RevocationMode.Online` — if a CRL/OCSP endpoint cannot be reached,
  the verifier cannot prove the cert has not been revoked.
- Any other status (including `NotTimeValid`) → `ChainBuildFailed`.

## 7. RFC 3161 timestamp

- The signer's `UnsignedAttributes` MUST contain
  `1.2.840.113549.1.9.16.2.14` (`id-aa-signatureTimeStampToken`).
- The token MUST decode via `Rfc3161TimestampToken.TryDecode`.
- `Rfc3161TimestampToken.VerifySignatureForSignerInfo` MUST succeed and
  return a TSA certificate; this cryptographically binds the token to
  the primary signature's `EncryptedDigest`.
- The TSA certificate MUST carry EKU exactly `1.3.6.1.5.5.7.3.8`
  (`id-kp-timeStamping`), exclusive of any other OID.
- The TSA certificate's **immediate issuer** Distinguished Name MUST
  consist of exactly the following RDNs (same comparison rules as §5.1).
  This pins the TSA leaf to a specific DigiCert timestamping
  intermediate as a defense-in-depth check on top of the chain build:

  | OID        | Short | Value                                                           |
  | ---------- | ----- | --------------------------------------------------------------- |
  | `2.5.4.3`  | `CN`  | `DigiCert Trusted G4 TimeStamping RSA4096 SHA256 2025 CA1`      |
  | `2.5.4.10` | `O`   | `DigiCert, Inc.`                                                |
  | `2.5.4.6`  | `C`   | `US`                                                            |

- The TSA certificate chain MUST build under the same rules as §6,
  except:
  - `CustomTrustStore` is the union of `options.TrustedTimestampRoots` +
    OS roots.
  - `ExtraStore` is the TSA token's own CMS certificate bag.
  - `ApplicationPolicy` MUST contain `1.3.6.1.5.5.7.3.8`
    (`id-kp-timeStamping`) instead of `id-kp-codeSigning`.
  - `VerificationTime` MUST be left at the chain engine's default
    (current UTC). The TSA certificate's validity period is what attests
    to *when* the timestamp was issued; anchoring the TSA chain's
    verification time to the timestamp it produces is chicken-and-egg.
    Mirrors NuGet's
    [`CertificateChainUtility.SetCertBuildChainPolicy`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/Signing/Utility/CertificateChainUtility.cs),
    which skips `VerificationTime` when `CertificateType == Timestamp`.
  - Failure mapping: a revoked TSA cert → `TimestampChainFailed`;
    revocation status unreachable → `TimestampRevocationUnavailable`;
    any other chain failure → `TimestampChainFailed`.

The "authoritative signing time" used everywhere else (cert validity
window, JSON policy) is `token.TokenInfo.Timestamp` (UTC), not the
signer's claimed signing-time attribute.

Failures: `TimestampMissing`, `TimestampMalformed`,
`TimestampBindingInvalid`, `TimestampEkuInvalid`,
`TimestampIssuerMismatch`, `TimestampChainFailed`,
`TimestampRevocationUnavailable`.

## 8. Signed attributes (PKCS#9, optional)

PKCS#7/CMS allows a v1 `SignerInfo` with no signed attributes — in which
case the signature value is computed directly over the encapsulated
content and is fully validated by §3 (`CheckSignature`). The verifier
treats PKCS#9 signed attributes as optional:

- If `SignedAttributes` is empty, the verifier accepts the signature on a
  pure PKCS#7 basis. No `ContentTypeAttributeInvalid`,
  `MessageDigestMismatch`, or `SigningTimeMissing` failure is emitted.
- If any signed attribute is present, RFC 5652 requires the signer to
  also include `content-type` and `message-digest`. The verifier enforces:

  | OID                         | Attribute        | Constraint                                                                                          |
  | --------------------------- | ---------------- | --------------------------------------------------------------------------------------------------- |
  | `1.2.840.113549.1.9.3`      | `content-type`   | Value MUST be `id-data` (`1.2.840.113549.1.7.1`).                                                   |
  | `1.2.840.113549.1.9.4`      | `message-digest` | MUST be present; value correctness is covered by `CheckSignature`.                                  |
  | `1.2.840.113549.1.9.5`      | `signing-time`   | OPTIONAL. If present, MUST be parseable and within ± 5 minutes of the TSA timestamp from §7.        |

Failures: `ContentTypeAttributeInvalid`, `MessageDigestMismatch`,
`SigningTimeMissing` (only emitted when a malformed `signing-time` value
is present — the name is historical; absence is not a failure),
`SigningTimeMismatch`.

## 9. JSON content policy (manifest only)

When the caller passes `RequireJsonExpirationField = true` (the default
for manifest callers; archive callers pass `false`), the verifier
additionally enforces:

- Content MUST parse as strict JSON (no comments, no trailing commas).
- Root MUST be a JSON object.
- The object MUST contain a string `expiration`, parseable as an
  ISO-8601 UTC `DateTimeOffset`. The verifier checks for a top-level
  `expiration` property first and falls back to `signature.expiration`
  (v2 manifest format).
- `signingTime < expiration` (TSA-authoritative signing time, not the
  attribute-claimed one).
- `UtcNow < expiration`.

Failures: `JsonParseFailed`, `ExpirationMissing`, `ExpirationMalformed`,
`SignedAfterExpiration`, `ExpiredNow`.

## 10. Result reporting

The verifier returns a `VerificationResult` aggregating
`VerificationFailure { FailureCode Code, string Reason }` entries.
`VerificationResult.IsValid` is `true` only when no failure entries were
recorded (skips do not count).

Two execution modes are supported via `VerificationMode`:

- **`ShortCircuit` (default)** — verifier returns on the first real
  failure. Production default; callers only need to know whether
  verification succeeded.
- **`CollectAll`** — verifier runs every check and reports every failure.
  Useful for diagnostics and tests that want full coverage in one run.

When a check cannot meaningfully run because a precondition failed (e.g.
CMS would not decode), the verifier emits a `CheckSkipped` entry naming
the missing precondition. Skipped entries are informational and do not
affect `IsValid`.

## 11. Non-goals

The verifier deliberately does not handle:

- Multiple signers or nested/parallel signatures.
- Counter-signatures other than the single RFC 3161
  `signatureTimeStampToken`.
- Certificate revocation discovery via AIA fetch of intermediates.
- Air-gapped / offline verification (see §1 / §6 TODOs).
- Non-PKCS#7/CMS container formats (PGP, XMLDSig, JWS, etc.).
