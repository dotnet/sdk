# dotnetup-in-SDK and the Signing-Policy Servicing Problem

This is a forward-looking note, not a description of current behavior. As of
today, **the .NET SDK knows nothing about `dotnetup`**: `dotnetup` is a separate,
self-contained binary that ships its own copy of `Microsoft.Dotnet.Installation`,
its own pinned trusted-root PEMs, and its own
[`SignatureVerifier`](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/Signing/SignatureVerifier.cs)
policy. Each released `dotnetup` is the policy authority for its own runs, and
the user is always expected to be on the latest `dotnetup` (one of `dotnetup`'s
first acts is to self-update; older builds are not a supported configuration).

That property — *only the latest `dotnetup` is supported* — is what makes the
strict, baked-in algorithm allow-list in
[`signature-verification.md`](signature-verification.md) §4 safe today. New
algorithms, new digest OIDs, new pinned issuer DNs, and new trusted-root PEMs
can be added in a regular SDK PR and reach customers on the normal `dotnetup`
self-update cadence. There is no SDK-installed copy of the verifier that has to
be reasoned about separately.

This document describes what would have to change *if that property goes away*
— specifically, if a future design embeds (or hard-bundles) `dotnetup` into the
.NET SDK such that customers can end up on an SDK-shipped `dotnetup` that is
older than the latest standalone `dotnetup` and is asked to verify a *newer*
release manifest.

## Why this matters

`SignatureVerifier`'s acceptance rules are intentionally hostile to unknown
inputs:

- The digest OID allow-list (`SignatureVerifier.DigestOids.cs` plus the pure-PQC
  OIDs in `SignatureVerifier.PqcOids.cs`) rejects anything not enumerated.
- The signature-algorithm allow-list (RSA-4096+ and the pure-PQC families)
  likewise rejects anything else.
- The signer-issuer and TSA-issuer DNs are pinned to a specific DigiCert
  intermediate CN (`SignatureVerifier.DistinguishedNames.cs`). When DigiCert
  rotates an intermediate, the pin has to be widened.
- The trusted-root PEMs (`src/Layout/redist/trustedroots/codesignctl.pem`,
  `timestampctl.pem`) are loaded as the *only* trust anchors — the OS root
  store is not merged in (§6 / §11 non-goal).

In the current model, all four lists are owned by the running `dotnetup`. If a
.NET release pipeline starts signing with a new digest (e.g. rotates SHA-384 →
SHA-512, or adopts a new PQC composite), an SDK PR updates the list, ships in
the next `dotnetup` build, and customers pick it up on next self-update *before*
they encounter a manifest signed with the new digest.

If `dotnetup` becomes part of the SDK and customers can be stuck on an older,
SDK-bundled `dotnetup`, that ordering breaks. A customer on an N-1 SDK could
download an N manifest signed with a digest, algorithm, or issuer their bundled
verifier was hard-coded to reject — and the install would fail with
`WeakDigest`, `SignatureAlgorithmNotPermitted`, `IssuerMismatch`, or
`ChainBuildFailed`, depending on what rotated.

## What a servicing mechanism would have to provide

Any future design that ships an SDK-bundled `dotnetup` needs an out-of-band
update path for at least the four sets above. Sketch of the design space (none
of these are committed; this is a checklist for whoever picks this up):

1. **Signed policy bundle, fetched at verify time.** A small, separately-signed
   "signing policy" file published alongside the release manifests. It contains
   the current allow-list of digest OIDs, signature-algorithm OIDs, pinned
   issuer DNs, and trusted-root certificates. The bundled `SignatureVerifier`
   loads the bundle, verifies it against a *bootstrap* trust anchor baked into
   the SDK, and uses the bundle's contents as the runtime allow-list. The
   bootstrap anchor itself has to be chosen to outlive the SDK's support
   window (currently 3 years for LTS).

2. **Make the bundled `dotnetup` a transient bootstrapper.** The SDK only ships
   enough of `dotnetup` to self-update to the latest standalone `dotnetup`, then
   immediately defers all signing-policy decisions to that newer binary. The
   SDK-bundled verifier only ever has to validate the *self-update* signature,
   which can be locked to a small, slow-rotating policy. This is the preferred
   option if it is workable, because it preserves today's "only latest
   `dotnetup` is policy" invariant.

3. **Service through SDK servicing releases.** Every supported SDK servicing
   branch updates `SignatureVerifier`'s allow-lists and pinned roots whenever
   a rotation happens upstream. Operationally expensive (a single TSA-cert
   rotation now requires N back-ports), and customers who are slow to take
   servicing updates remain exposed. Only viable as a stopgap.

4. **Permit multiple `SignerInfo`s and let the release pipeline parallel-sign.**
   Per `dtivel`'s review on
   [#54300](https://github.com/dotnet/sdk/pull/54300#discussion_r3331278636),
   allowing parallel signatures (e.g. legacy RSA + new PQC over the same
   content) lets a single manifest satisfy both an old SDK-bundled verifier
   and a new standalone one. This relaxes §2's "exactly one `SignerInfo`"
   rule and §7's single-token assumptions; both would have to be revisited.
   Realistically a complement to one of the above, not a replacement.

Whichever option is chosen, two acceptance-test changes from the current model
are required:

- A test must enumerate the version skew window the design supports (`SDK
  version X` ↔ `manifest signed at time Y`) and validate the verifier inside
  X's bundled `dotnetup` accepts a manifest from Y. Today there is no skew —
  Y is always "today's standalone build" — so this test does not exist.
- The expiration-window test added alongside this doc
  (`PinnedIssuerCertificates_HaveAtLeast90DaysUntilExpiration` in
  [`test/dotnetup.Tests/SignatureVerifierTests.cs`](../../../test/dotnetup.Tests/SignatureVerifierTests.cs))
  becomes load-bearing rather than advisory: under the current model an
  expired issuer just means "ship a `dotnetup` update before customers
  notice"; under a bundled-`dotnetup` model it means "ship a servicing
  update to every supported SDK band before customers notice".

## Until then

No action is required in current `dotnetup` PRs beyond:

1. Updating `SignatureVerifier.DistinguishedNames.cs`, the trusted-root PEMs,
   and the digest / algorithm allow-lists whenever the .NET Release pipeline
   rotates them.
2. Keeping the 90-day pinned-issuer expiration test (see
   [signature-verification.md](signature-verification.md) §4) green, which
   gives the team early warning to push a `dotnetup` update before a pinned
   intermediate's `notAfter` lapses.

When (if) someone proposes embedding `dotnetup` in the SDK, this document is
the open issue list that proposal needs to close.
