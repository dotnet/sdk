# Self-update verification

Self-update checks the published SHA-512 hash and the executable's `--version` output. The
channel feed is not a signed manifest, so self-update emits an unsigned-source warning and respects
the unsigned-download policy. On Windows, when the installed dotnetup is Authenticode-signed, the
downloaded executable must also have a valid Authenticode signature that chains to a Microsoft root.
Otherwise the staged file is deleted and the command fails with `SignatureVerificationFailed`
before replacement. Unsigned installations, such as local builds, and Linux and macOS rely on the
hash alone. The selected release must publish the executable and checksum; no identity
sidecar or custom embedded version record is needed. After replacement, `--version`
must run successfully and report the selected release's version within a bounded
timeout. Build metadata must match exactly if the feed specifies it; otherwise a
source revision suffix in the informational version is permitted. These unsigned checks
do not authenticate release freshness.

Signed version manifests and monotonic authorization are deferred to future stages.
