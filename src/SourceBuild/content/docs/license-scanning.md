# License Scanning

The VMR is regularly scanned for license references to ensure that only open-source license are used where relevant.

License scanning pipline: https://dev.azure.com/dnceng/internal/_build?definitionId=1301 (internal only)

License scanning test: https://github.com/dotnet/dotnet/blob/main/test/Microsoft.DotNet.SourceBuild.SmokeTests/LicenseScanTests.cs

By default, running the pipeline will scan all repos within the VMR which takes several hours to run.
The pipeline can be triggered manually to target a specific repo within the VMR by setting the `specificRepoName` parameter.
This value should be the name of the repo within the VMR (i.e. a name of a directory within https://github.com/dotnet/dotnet/tree/main/src).
To test source modifications intended to resolve a license issue, apply the change in an internal branch of the VMR.
Run this pipeline, targeting your branch, and set the `specificRepoName` parameter to the name of the repo containing the change.

The output of the pipeline is a set of test results and logs.
The logs are published as an artifact and can be found at test/Microsoft.DotNet/SourceBuild.SmokeTests/bin/Release/netX.0/logs.
It consists of the following:
  * `UpdatedLicenses.<repo-name>.json`: This is the output of that gets compared to the stored baseline.
    If they're the same, the test passes; if not, it fails. By comparing this file to the baseline, one can determine which new license
    references have been introduced.
    If everything is deemed to be acceptable, the developer can either update the allowed licenses, update the exclusions file, update the
    baseline, or any combination.
  * `scancode-results.json`: This is the raw output that comes from scancode. This file is useful for diagnostic purposes because it tells you
    the exact line number of where a license has been detected in a file.
