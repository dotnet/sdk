# Source Build Smoke Tests

* Run these tests via `build.sh --test`

The following properties are automatically available during test execution but can be overwritten:
- PoisonUsageReportFile
- SdkTarballPath
- SourceBuiltArtifactsPath

Optional msbuild properties:
- MsftSdkTarballPath
- SmokeTestsCustomSourceBuiltPackagesPath
- SmokeTestsExcludeOmniSharpTests
- SmokeTestsLicenseScanPath
- SmokeTestsPrereqsPath
- SmokeTestsWarnOnLicenseScanDiffs
- SmokeTestsWarnOnSdkContentDiffs

Make sure to rebuild the test project when changing one of those values.

## Dependencies

Some tests need additional dependencies. These must be installed (manually and separately) on the system for the tests to pass.

The following programs are used by some tests:

- eu-readelf
- file

## Prereq Packages

Some prerelease scenarios, usually security updates, require non-source-built packages which are not publicly available.
Specify the directory where these packages can be found via the `SmokeTestsPrereqsPath` msbuild property when running tests via `build.sh ---test` e.g.
`/p:SmokeTestsPrereqsPath=prereqs/packages/smoke-test-prereqs`.
