# Source Build Smoke Tests

Run these tests via `build.sh -sb --test`

See the [Microsoft.DotNet.SourceBuild.Tests.csproj](Microsoft.DotNet.SourceBuild.Tests.csproj) for the available
RuntimeHostConfigurationOptions that can be used to configure the tests.

## Dependencies

Some tests need additional dependencies. These must be installed (manually and separately) on the system for the tests to pass.

The following programs are used by some tests:

- eu-readelf
- file

## Prereq Packages

Some prerelease scenarios, usually security updates, require non-source-built packages which are not publicly available.
You can specify a custom nuget feed for where these packages can be loaded from via the `SourceBuildTestsCustomSourceBuiltPackagesPath`
 msbuild property when running tests via `build.sh ---test` e.g. `/p:SourceBuildTestsCustomSourceBuiltPackagesPath=<FEED URL OR LOCAL PATH>`.
