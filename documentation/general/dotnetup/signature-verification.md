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
`TrustedRootsEmpty`. Because the chain build's `CustomTrustStore` is exactly the supplied
roots (no OS-store union; see §6), an empty trusted-root collection would otherwise leave
the chain engine with no anchors at all and every chain build would fail with a generic
status — surfacing `TrustedRootsEmpty` makes the misconfiguration obvious.

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

Failures: `SigDecodeFailed`, `SigMultipleSigners`, `SignerCertMissing`.

## 3. Cryptographic integrity

The verifier calls `SignedCms.CheckSignature(verifySignatureOnly: true)`.
Trust is evaluated separately in §6, because the verifier enforces a
custom trust store (§5) that cannot be expressed via `CheckSignature`'s
built-in chain build.

Failure: `SigCryptoInvalid`.

## 4. Algorithm policy

The verifier accepts the following classical and post-quantum algorithm families.
Within each family, exact OIDs are listed in `SignatureVerifier.cs`.

**Digest algorithm** (CMS `SignerInfo.digestAlgorithm`):

- SHA-2: SHA-256, SHA-384, SHA-512.
- SHA-3: SHA3-256, SHA3-384, SHA3-512.
- SHAKE: SHAKE-128, SHAKE-256 (required for ECDSA-with-SHAKE per RFC 8692 and
  for SHAKE-flavoured SLH-DSA variants).
- Pure-PQC algorithm OIDs reused as digest identifiers (see below).
- SHA-1 / MD5 are rejected.

**Signature algorithm** (signer certificate's `SubjectPublicKeyInfo.algorithm`):

- Classical: RSA (`1.2.840.113549.1.1.1`), ECDSA (`1.2.840.10045.2.1`).
- Post-quantum (per FIPS 204 / FIPS 205 and `draft-ietf-lamps-cms-ml-dsa` /
  `draft-ietf-lamps-pq-composite-sigs`, supported by .NET 11's
  `System.Security.Cryptography.MLDsa` / `SlhDsa` / `CompositeMLDsa`):
  - **ML-DSA** — ML-DSA-44, ML-DSA-65, ML-DSA-87 (and their pre-hash variants).
  - **SLH-DSA** — all twelve FIPS-205 variants (SHA2 / SHAKE × 128/192/256 × s/f),
    pre-hash variants included.
  - **Composite ML-DSA** — the 18 hybrid algorithms registered under OID arc
    `1.3.6.1.5.5.7.6.37`–`54` (ML-DSA paired with RSA-PSS / RSA-PKCS#15 / ECDSA / EdDSA).
- DSA and any other public-key algorithm are rejected.

For pure-PQC signatures, the algorithm OID is repurposed as the `digestAlgorithm`
identifier per `draft-ietf-lamps-cms-ml-dsa` (the signature scheme does its own
internal hashing). The verifier therefore accepts the PQC OIDs in *both* the
digest and public-key allow-lists; `SignedCms.CheckSignature` performs the actual
cryptographic verification.

Failures: `WeakDigest`, `SignatureAlgorithmNotPermitted`.

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

Pinning the signer's immediate issuer by DN is **not** the cryptographic trust anchor for
the primary signature — that is the chain build in §6 against the pinned roots in
`codesignctl.pem`. The DN pin is a *defense-in-depth* check that the cert was issued by
the specific DigiCert code-signing intermediate the .NET Release signer is configured to
use.

The signer certificate's **immediate issuer** Distinguished Name MUST consist of exactly
the following RDNs (same comparison rules as §5.1):

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
- `CustomTrustStore` = **exactly** `options.TrustedCodeSigningRoots` (the pinned PEMs in
  `codesignctl.pem`). The OS root store is intentionally NOT merged in: the bundled CTL
  already contains the DigiCert root anchors the .NET Release signer chains to, and
  augmenting them with the system store would silently widen trust beyond what the pinned
  CTL declares and reintroduce a snapshot-vs-live consistency problem on long-lived
  processes if the system store is rotated. See §11 (non-goals).
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

- The signer's `UnsignedAttributes` MUST contain **at least one**
  `1.2.840.113549.1.9.16.2.14` (`id-aa-signatureTimeStampToken`). RFC 5126 §6.1.1
  explicitly permits the attribute to appear multiple times — each instance is a
  separate TSA witness over the same `SignerInfo.signatureValue` (typically used for
  renewal as an original TSA cert ages towards expiry).
- When multiple tokens are present the verifier validates **all** of them: every token
  MUST decode via `Rfc3161TimestampToken.TryDecode`, every token's
  `Rfc3161TimestampToken.VerifySignatureForSignerInfo` MUST succeed, and every TSA
  chain MUST build under the rules below. Failing any single token invalidates the
  signature.
- The **earliest** `token.TokenInfo.Timestamp` across all valid tokens is used as the
  authoritative signing time for the primary chain's `VerificationTime` (§6) and the
  JSON expiration check (§9). Earliest is the most conservative choice: the signer
  cert had to be valid at that point, and later renewal tokens only *extend* trust
  into the future, never relax it. Because every token is fully validated, accepting
  additional tokens cannot weaken the signature — each one is another mandatory check.
- Each TSA certificate MUST carry EKU exactly `1.3.6.1.5.5.7.3.8`
  (`id-kp-timeStamping`), exclusive of any other OID, per RFC 3161 §2.3
  ("the certificate MUST contain only one instance of the extended key
  usage field extension … with KeyPurposeID having value:
  id-kp-timeStamping"). The CAB-Forum BR §7.1.2.3(f) "MAY be present"
  permitted-extras list applies only to code-signing certificates, not to
  timestamp certificates — so the TSA EKU policy is intentionally
  stricter than the primary signer's CAB-Forum-aligned policy in §5.3.
- Each TSA certificate's **immediate issuer** Distinguished Name MUST
  consist of exactly the following RDNs (same comparison rules as §5.1).
  This pins the TSA leaf to a specific DigiCert timestamping
  intermediate as a defense-in-depth check **on top of** §6's chain
  build (which is the cryptographic trust anchor; the DN pin protects
  against a CA misconfiguration that lets a different DigiCert-rooted
  timestamping leaf chain successfully):

  | OID        | Short | Value                                                           |
  | ---------- | ----- | --------------------------------------------------------------- |
  | `2.5.4.3`  | `CN`  | `DigiCert Trusted G4 TimeStamping RSA4096 SHA256 2025 CA1`      |
  | `2.5.4.10` | `O`   | `DigiCert, Inc.`                                                |
  | `2.5.4.6`  | `C`   | `US`                                                            |

  > **Note:** Today the verifier accepts exactly one TSA-issuer CN.
  > DigiCert (and any future TSA provider) rotates these intermediates
  > periodically — when that happens, this single-value pin will be
  > generalized to an allow-list of accepted CNs (each entry still
  > requiring exact match against the same `O` / `C` RDNs). The intent
  > stays the same: the TSA leaf must be issued by a known, named
  > intermediate, not just any cert that chains to a trusted timestamping
  > root.

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
window, JSON policy) is `token.TokenInfo.Timestamp` (UTC). The PKCS#9
`signing-time` signed attribute is intentionally not consulted (see §8).

> **TSA-cert lifetime vs. release cadence.** The .NET release pipeline re-signs the
> primary release manifests on every release cycle (currently monthly), and the bundled
> `timestampctl.pem` plus DigiCert's TSA-cert rotation cadence is comfortably longer than
> that window plus normal client cache lifetimes. The standard code-signing concern about
> TSA-cert expiration — where artifacts are immutable and new TSTs must be layered over
> old ones as the original TSA cert ages — does not apply: a manifest whose TSA leaf has
> expired is by definition stale and the next monthly release supersedes it. Older
> manifests being invalidated by TSA expiry is intended behavior here and aligns with
> Liquid `Microsoft.Security.SystemsADM.10053`. (This concerns *manifests* only — the
> archives themselves are integrity-bound by the manifest's SHA-512 pins rather than
> by direct CMS signatures; archive `.p7s` verification is out of scope today and
> appears in §1's TODO list.)

Failures: `TimestampMissing`, `TimestampMalformed`,
`TimestampBindingInvalid`, `TimestampEkuInvalid`,
`TimestampIssuerMismatch`, `TimestampChainFailed`,
`TimestampRevocationUnavailable`.

## 8. Signed attributes (PKCS#9, intentionally not checked)

PKCS#7/CMS allows a v1 `SignerInfo` with no signed attributes — in which case the
signature value is computed directly over the encapsulated content and is fully
validated by §3 (`CheckSignature`). Production .NET release signatures use exactly
this form: zero `SignedAttributes`, with the RFC 3161 TST in `UnsignedAttributes`.

The verifier therefore does not inspect PKCS#9 signed attributes at all:

- Cryptographic integrity is covered by `SignedCms.CheckSignature` (§3), which itself
  enforces the RFC 5652 §5.4 / §11 rules for `content-type` / `message-digest` when
  signed attributes are present.
- The `signing-time` attribute (`1.2.840.113549.1.9.5`) is **not** consulted, even when
  present. RFC 3161 timestamps prove "no later than" and may legitimately be added
  after the signer's claimed signing-time (per RFC 5126 §6.1.1 renewal); enforcing
  `signing-time ≤ TSA-time` would treat a self-claim as authoritative against an
  independently witnessed cryptographic timestamp, and would also create a perverse
  incentive (omit `signing-time` to escape a check that only applies if you include
  it). The authoritative signing time used everywhere else in this spec
  (chain `VerificationTime` in §6, JSON expiration in §9) is `token.TokenInfo.Timestamp`
  from §7, never `signing-time`.

No failures are emitted from this section.

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
- `signingTime < expiration` (TSA-authoritative signing time per §7).
- `UtcNow < expiration`.

Failures: `JsonParseFailed`, `ExpirationMissing`, `ExpirationMalformed`,
`SignedAfterExpiration`, `ExpiredNow`.

## 10. Result reporting

The verifier returns a `VerificationResult` aggregating
`VerificationFailure { FailureCode Code, string Reason }` entries.
`VerificationResult.IsValid` is `true` only when no entries (failures or
skips) were recorded. Skips are stored in the same list as failures and
count against `IsValid` — see the §10 paragraph below for rationale.

Two execution modes are supported via `VerificationMode`:

- **`ShortCircuit` (default)** — verifier returns on the first recorded
  entry, whether failure or `CheckSkipped`. Production default; callers
  only need to know whether verification succeeded.
- **`CollectAll`** — verifier runs every check and reports every failure.
  Useful for diagnostics and tests that want full coverage in one run.

When a check cannot meaningfully run because a precondition failed (e.g.
CMS would not decode), the verifier emits a `CheckSkipped` entry naming
the missing precondition. Skipped entries are appended to the same
`Failures` list as real failures and do contribute to `IsValid`: a
`CheckSkipped` only ever appears when an upstream failure already removed
its input, so the upstream failure has already invalidated the result —
the skip is the diagnostic breadcrumb explaining which downstream checks
could not be evaluated as a consequence. Treating skips as
non-invalidating would let a result with a `CheckSkipped` (and no other
entries) report `IsValid == true`, which is never correct: the only path
to a `CheckSkipped` is through a prior failure.

## 11. Non-goals

The verifier deliberately does not handle:

- Multiple signers or nested/parallel signatures.
- Counter-signatures other than RFC 3161 `signatureTimeStampToken` (which may itself
  appear multiple times for TSA renewal per RFC 5126 §6.1.1 — see §7).
- Certificate revocation discovery via AIA fetch of intermediates.
- Air-gapped / offline verification (see §1 / §6 TODOs).
- Non-PKCS#7/CMS container formats (PGP, XMLDSig, JWS, etc.).
- Augmenting `CustomTrustStore` with the OS root store. The pinned PEMs
  in `codesignctl.pem` / `timestampctl.pem` already include the trust
  anchors the .NET Release signer chains to; layering the OS-store union
  on top would silently widen accepted trust beyond what the pinned CTLs
  declare. See §6.
