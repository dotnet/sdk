# Source Build Smoke Tests

* Run these tests via `build.sh --run-smoke-test`
* Various configuration settings are stored in `Config.cs`

## Prereq Packages

Some prerelease scenarios, usually security updates, require non-source-built packages which are not publicly available.
Specify the directory where these packages can be found via the `SMOKE_TESTS_PREREQS_PATH` environment variable when running tests via `build.sh --run-smoke-test` e.g.
`SMOKE_TESTS_PREREQS_PATH=prereqs/packages/smoke-test-prereqs`.
