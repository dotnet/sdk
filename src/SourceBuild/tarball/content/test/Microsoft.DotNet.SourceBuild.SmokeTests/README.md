# Source Build Smoke Tests

* Run these tests via `build.sh --run-smoke-test`
* Various configuration settings are stored in `Config.cs`

## Prereq Packages
Some prerelease scenarios, usually security updates, require non-source-built packages which are not publicly available.
Place these packages in the tarball's `packages/smoke-test-prereqs`. When prereq packages are required, the
`EXCLUDE_ONLINE_TESTS=true` environment variable should be set when running tests via `build.sh --run-smoke-test`.
