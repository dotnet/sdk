# dotnetup Signature Verification

This document describes the detached CMS / PKCS#7 signatures (`.p7s`)
that dotnetup accepts when verifying .NET release artifacts (manifest
JSON files and release archives such as `.zip` / `.tar.gz`).

Descriptive prose explains what the in-tree
verifier does and why; the **MUST** clauses are normative — a signature
that fails any of them is not a valid .NET release signature.

The verifier is implemented by the internal `SignatureVerifier` class in
the `Microsoft.Dotnet.Installation` library. It is cross-platform (Windows,
Linux, macOS) and uses only the BCL (`System.Security.Cryptography.Pkcs`,
`System.Security.Cryptography.X509Certificates`, `System.Text.Json`).
The only content-specific check (§9, JSON expiration) is opt-in via
`SignatureVerificationOptions.RequireJsonExpirationField`.

Although the verifier lives in `Microsoft.Dotnet.Installation` and is
named for dotnetup, the acceptance rules below apply to any tool that
consumes a `.p7s` covering a .NET release artifact — for example the
install scripts (`dotnet-install.sh` / `dotnet-install.ps1`) or other
in-house installers. Such consumers may layer additional concerns on
top (e.g. offline-revocation support for air-gapped scenarios).

> Manifest verification covers archives transitively: the
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

The `Microsoft.Dotnet.Installation` library bundles `codesignctl.pem` and
`timestampctl.pem` (sourced from `src/Layout/redist/trustedroots/`) and
loads them as the default trust anchors via `DefaultSignatureOptions`. These are automatically synced via SDK codeflow from `main`.

When either trusted-root collection is empty, the verifier emits
`TrustedRootsEmpty`.

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

The verifier accepts the following classical and post-quantum algorithm families based upon what the dotnet runtime supports: https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Security/Cryptography/Oids.cs

There is a test hook which will warn if the runtime supports new algorithms we do not yet support - but since we are only trying to validate a specific signature where the algorithm will not change without notice, this could be considered overkill.

Within each family, exact OIDs are listed in `SignatureVerifier.cs`.

**Digest algorithm** (CMS `SignerInfo.digestAlgorithm`):

- SHA-2: SHA-256, SHA-384, SHA-512.
- SHA-3: SHA3-256, SHA3-384, SHA3-512.
- SHAKE: SHAKE-128, SHAKE-256 (required for SHAKE-flavoured SLH-DSA variants).
- Pure-PQC algorithm OIDs reused as digest identifiers (see below).
- SHA-1 / MD5 are rejected.

**Signature algorithm** (signer certificate's `SubjectPublicKeyInfo.algorithm`):

`SubjectPublicKeyInfo` (SPKI) is the X.509 ASN.1 structure ([RFC 5280
§4.1](https://datatracker.ietf.org/doc/html/rfc5280#section-4.1)) that
carries a certificate's public key:

```asn1
SubjectPublicKeyInfo  ::=  SEQUENCE  {
    algorithm         AlgorithmIdentifier,
    subjectPublicKey  BIT STRING  }

AlgorithmIdentifier   ::=  SEQUENCE  {
    algorithm   OBJECT IDENTIFIER,
    parameters  ANY DEFINED BY algorithm OPTIONAL  }
```

Concretely, every X.509 certificate has exactly one SPKI field; for an
RSA certificate the OID is `rsaEncryption` (`1.2.840.113549.1.1.1`) and
the bit string is the DER-encoded RSA public key; for an ML-DSA-65
certificate the OID is `id-ml-dsa-65` (`2.16.840.1.101.3.4.3.18`) and
the bit string is the raw ML-DSA encoded public key. The OID inside
SPKI identifies the *key type*, not a particular signing operation —
the signing-time choice (e.g. pure vs. pre-hash, RSA-PSS vs.
RSA-PKCS#1 v1.5) is carried separately in
`SignerInfo.signatureAlgorithm` on each individual signature.

The verifier accepts only the following SPKI algorithm OIDs:

- **RSA** (`1.2.840.113549.1.1.1`) — with the additional constraint that the
  RSA public key's modulus MUST be **at least 4096 bits**. Smaller RSA keys
  (RSA-2048, RSA-3072) are rejected as `WeakSignatureKey`.

- **Post-quantum** pure-mode OIDs only (ML-DSA, SLH-DSA, Composite ML-DSA).
  Pre-hash PQC variants such as pure ECDSA are rejected. Windows does not fully support ECC/ECDSA. https://learn.microsoft.com/en-us/security/trusted-root/program-requirements#b-signature-requirements

For pure-PQC signatures the algorithm OID is repurposed as the CMS
`digestAlgorithm` identifier — per
[`draft-ietf-lamps-cms-ml-dsa`](https://datatracker.ietf.org/doc/draft-ietf-lamps-cms-ml-dsa/)
the signature scheme does its own internal hashing, so CMS uses the same
OID in both positions and a separate digest is unnecessary. The verifier
therefore accepts pure-mode PQC OIDs in *both* the digest and
public-key allow-lists, and the pre-hash PQC OIDs in the digest
allow-list only (since per the SPKI restriction above they have no
place on a certificate's public-key field).
`SignedCms.CheckSignature` performs the actual cryptographic
verification.

Failures: `WeakDigest`, `WeakSignatureKey`, `SignatureAlgorithmNotPermitted`.

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

The DN pin below is **not** the cryptographic trust anchor for the primary signature —
that is the §6 chain build against the pinned roots in `codesignctl.pem`. It is a
defense-in-depth check that the cert was issued by the specific DigiCert code-signing
intermediate the .NET Release signer is configured to use.

The signer certificate's **immediate issuer** Distinguished Name MUST consist of exactly
the following RDNs (same comparison rules as §5.1):

| OID        | Short | Value                                                       |
| ---------- | ----- | ----------------------------------------------------------- |
| `2.5.4.3`  | `CN`  | `DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1`  |
| `2.5.4.10` | `O`   | `DigiCert, Inc.`                                            |
| `2.5.4.6`  | `C`   | `US`                                                        |

Failure: `IssuerMismatch`.

### 5.3 Extended Key Usage

Follows CA/Browser Forum Code Signing Baseline Requirements
[§7.1.2.3(f)](https://cabforum.org/working-groups/code-signing/requirements/#7123-code-signing-and-timestamp-certificate):

- The signer certificate MUST carry **exactly one** EKU extension. RFC 5280 §4.2.1.12
  encodes multiple KeyPurposeID values as a sequence inside a single extension; carrying
  two EKU extensions is non-conformant.
- The EKU MUST contain `1.3.6.1.5.5.7.3.3` (`id-kp-codeSigning`).
- The EKU MUST NOT contain `2.5.29.37.0` (`anyExtendedKeyUsage`) or
  `1.3.6.1.5.5.7.3.1` (`id-kp-serverAuth`).
- The EKU MAY also contain any of the CAB-Forum-permitted extras for code-signing
  certificates:
  - `1.3.6.1.4.1.311.10.3.13` (Microsoft Authenticode Lifetime Signing)
  - `1.3.6.1.5.5.7.3.4` (`id-kp-emailProtection`)
  - `1.3.6.1.4.1.311.3.10.3.12` (Microsoft Document Signing)
- Any other OID is rejected.

Failures: `EkuMissing`, `EkuNotExclusiveCodeSign`, `EkuMultipleExtensions`.

The TSA cert (§7) follows a stricter `id-kp-timeStamping`-exclusive profile per RFC 3161 §2.3.

## 6. Certificate chain

The verifier builds an `X509Chain` for the signer certificate with:

- `TrustMode = CustomRootTrust`.
- `CustomTrustStore` = **exactly** `options.TrustedCodeSigningRoots` (the pinned PEMs in
  `codesignctl.pem`). The OS root store is intentionally NOT merged in. See §11 (non-goals).
- `ExtraStore` = the CMS certificate bag from §2. Intermediate
  certificates for both the primary signature and the timestamp signature
  (§7) MUST be embedded in their respective CMS certificate bags; the
  verifier does not follow AIA URLs.

- `RevocationMode` is configurable via `options.RevocationMode`:
  - `Online` (default, recommended): CRL/OCSP MUST succeed; fail closed
    when unreachable.
  - `Offline`: use locally cached CRLs only.
  - `NoCheck`: skip revocation entirely. Air-gap / offline scenarios only;
    the caller is responsible for bounding trust some other way.
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
  `1.2.840.113549.1.9.16.2.14` (`id-aa-signatureTimeStampToken`). RFC 3161 §2.4.2
  defines the attribute as a SET OF `TimeStampToken`, so the attribute MAY appear with
  multiple tokens — each instance is a separate TSA witness over the same
  `SignerInfo.signatureValue` (typically used for renewal as an original TSA cert ages
  towards expiry, in the same long-term-validation spirit as CAdES-A
  `archive-time-stamp` renewal per RFC 5126 §6.1).
- When multiple tokens are present the verifier validates **all** of them: every token
  MUST decode via `Rfc3161TimestampToken.TryDecode`, every token's
  `Rfc3161TimestampToken.VerifySignatureForSignerInfo` MUST succeed, and every TSA
  chain MUST build under the rules below. Failing any single token invalidates the
  signature.
- The **earliest** `token.TokenInfo.Timestamp` across all valid tokens is used as the
  authoritative signing time for the primary chain's `VerificationTime` (§6) and the
  JSON expiration check (§9). Earliest is the most conservative anchor: the signer
  cert had to be valid at that point, and later renewal tokens only extend trust
  into the future. Because every token is fully validated, additional tokens cannot
  weaken the signature — each is another mandatory check.
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
  - `CustomTrustStore` is exactly `options.TrustedTimestampRoots` (the pinned PEMs in
    `timestampctl.pem`). The OS root store is **not** merged in — same rationale as
    §6 and the non-goal in §11.
  - `ExtraStore` is the TSA token's own CMS certificate bag.
  - `ApplicationPolicy` MUST contain `1.3.6.1.5.5.7.3.8`
    (`id-kp-timeStamping`) instead of `id-kp-codeSigning`.
  - `VerificationTime` for each TST's *historical* chain build MUST be
    the TST's own `genTime` (`token.TokenInfo.Timestamp`), not wall-clock
    "now". This is what lets a release stay verifiable after its TSA leaf
    cert's `notAfter` passes — the TSA cert only needed to be valid at
    the moment it stamped the signature, not at every future
    verification. It is not circular: §7's decode step already called
    `Rfc3161TimestampToken.VerifySignatureForSignerInfo`, cryptographically
    proving the TSA cert signed the `TSTInfo` that carries this `genTime`,
    so `genTime` is authoritative content rather than unverified
    self-assertion. This is the standard PKI long-term-validation
    pattern (RFC 3161 page 15; same intent as the CAdES-A
    `archive-time-stamp` renewal pattern in RFC 5126 §6.1). Each TST in
    a multi-TST set is evaluated independently at its own `genTime`
    (RFC 3161 §2.4.2 permits a SET OF `TimeStampToken` in the
    `id-aa-signatureTimeStampToken` unsigned attribute).

The "authoritative signing time" used everywhere else (cert validity
window, JSON policy) is `token.TokenInfo.Timestamp` (UTC). The PKCS#9
`signing-time` signed attribute is intentionally not consulted (see §8).

> **TSA-cert lifetime vs. release cadence.** The .NET release pipeline re-signs the
> primary release manifests on every release cycle (currently monthly), and DigiCert's
> TSA-cert rotation cadence is comfortably longer than that window plus normal client
> cache lifetimes — so under steady-state network access a client almost never sees a
> manifest whose TSA leaf has expired. Because each TSA chain is evaluated at the
> TST's own `genTime` (see the rule bullets above), a manifest whose TSA leaf is now
> past `notAfter` still verifies cleanly as long as the leaf was valid when it issued
> the timestamp; offline clients, stale caches, and image-baked manifests stay
> verifiable across TSA-cert rotation. (This section concerns *manifests* only — the
> archives themselves are integrity-bound by the manifest's SHA-512 pins rather than
> by direct CMS signatures today.)

> One known gap remains: **TSA-cert revocation strictly between `genTime` and the next
> TST renewal** (e.g. key compromise published only after a TST was issued, and the
> manifest has not yet been re-timestamped). Chain-builds anchored at historical
> `genTime` cannot see CRL / OCSP entries dated later than `genTime`. The standard
> mitigation is archival revocation evidence pinned into the signature (RFC 5126 ATSv3
> / `RevocationValues`); we do not yet collect or evaluate it. Acceptable in current
> scope because (a) §9 bounds the post-`genTime` window to roughly one release cycle,
> (b) the greatest-`genTime` chain-at-now check forces the *current* TSA cert to be
> unrevoked, and (c) DigiCert revocation of an actively-used `.NET Release` TSA leaf
> would itself trigger a re-sign + new manifest within that window.

Failures: `TimestampMissing`, `TimestampMalformed`,
`TimestampBindingInvalid`, `TimestampEkuInvalid`,
`TimestampIssuerMismatch`, `TimestampChainFailed`,
`TimestampRevocationUnavailable`.

## 8. Signed attributes (PKCS#9, intentionally not checked)

Production .NET release signatures use exactly
this form: zero `SignedAttributes`, with the RFC 3161 TST in `UnsignedAttributes`.

The verifier therefore does not inspect PKCS#9 signed attributes at all.

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
count against `IsValid` — see the paragraph below for rationale.

Two execution modes are supported via `VerificationMode`:

- **`ShortCircuit` (default)** — verifier returns on the first recorded
  entry, whether failure or `CheckSkipped`. Production default; callers
  only need to know whether verification succeeded.
- **`CollectAll`** — verifier runs every check and reports every failure.
  Useful for diagnostics and tests that want full coverage in one run.

When a check cannot meaningfully run because a precondition failed (e.g.
CMS would not decode), the verifier emits a `CheckSkipped` entry naming
the missing precondition. `CheckSkipped` entries are appended to the same
`Failures` list as real failures and count against `IsValid`.

## 11. Non-goals

The verifier deliberately does not handle:

- Multiple signers or nested/parallel signatures.
- Counter-signatures other than RFC 3161 `signatureTimeStampToken` (which may itself
  appear multiple times for TSA renewal per RFC 3161 §2.4.2 — see §7).
- Certificate revocation discovery via AIA fetch of intermediates.
- Air-gapped / offline verification (see §1 / §6).
