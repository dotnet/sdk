# Unified Build Validation Tests

This project compares the output of the Unified Build system with the official Microsoft Build. There are tests to compare the list of files in the SDK archive, and the versions of each managed assembly in each SDK.
Files can be excluded from each comparison in by placing the file name in one of the files in the `assets/` folder. Each file will apply to a all or a subset of RIDs depending on the suffix of the filename. For example, filenames in SdkFileDiffExclusions-linux-any.txt will be excluded from all linux SDK comparisons.

| File Name                            | Purpose                                                                                                                |
|--------------------------------------|------------------------------------------------------------------------------------------------------------------------|
| SdkFileDiffExclusions.txt            | Files that should not be included in any file comparisons                                                              |
| NativeDlls.txt                       | Files that end in .dll or .exe that are not managed assemblies and should be excluded from assembly version validation |
| SdkAssemblyVersionDiffExclusions.txt | Files that should not be included in the assembly version validation for any other reason                              |

## SDK archive file list comparison

The files of each sdk archive iterated through, filtered and the name is written to `msftSdkFiles.txt` or `ubSdkFiles.txt` in the same directory as the test assembly. Then the test runs `git diff --no-index msftSdkFiles.txt ubSdkFiles.txt` and writes stdout to `$(OutDir)/log/UpdatedMsftToUbSdkFiles-{Rid}.diff`. This diff is then compared with the expected baseline in `./assets/baselines/MsftToUbSdkFiles-{Rid}.diff`. If the baselines are not identical, the test fails.

### Updating the baseline

If the test fails, but you've inspected the `UpdatedMsftToUbSdkFiles-{RID}.diff` and the changes are expected, then overwrite `./assets/baselines/MsftToUbSdkFiles-{Rid}.diff` with the updated baseline diff.

## Sdk assembly version comparison

This is done the similarly to the file comparison, but only looks at files that end in `.dll` or `.exe`, and filters out filename in `SdkFileDiffExclusions.txt`, `NativeDlls.txt`, and `SdkAssemblyVersionDiffExclusions.txt`. Each file name and the assembly version are written to `ub_assemblyversions.txt` or `msft_assemblyversions.txt`. Those files are then `git diff`'ed, and stdout is written to `$(OutDir)/log/UpdatedMsftToUbSdkAssemblyVersions-{Rid}.diff`. Then, the test diffs that with the baseline.

### Updating the baseline

If the test fails and the changes to the baseline are expected, the overwrite `./assets/baselines/MsftToUbSdkFiles-{RID}.diff` with the updated baseline.
