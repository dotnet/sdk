# Temporary ORAS.NET source dependency

The SDK needs upload and authentication changes from
[ORAS.NET #428](https://github.com/oras-project/oras-dotnet/pull/428) and
[#468](https://github.com/oras-project/oras-dotnet/pull/468).
`SourceVersion.props` records the reviewed commit. `Source/` contains its library
sources, with a provenance comment added to each file. The Apache 2.0 license is
included in `LICENSE` and the SDK's third-party notices.

Builds compile the checked-in sources without fetching anything from GitHub.
This also applies to clean offline and source-only builds after package restore.
`eng/vendored-files.json` tracks the upstream files through the repository's
vendored-source infrastructure.

To update, review and commit the changes in a local ORAS checkout, then run from
the SDK root:

```sh
python3 eng/update-oras-source.py /path/to/oras-dotnet <full-reviewed-commit-sha>
python3 .github/scripts/check_vendored_files.py validate
```

Do not edit generated files under `Source/` or `SourceVersion.props`. Fix the
upstream source and run the generator again. Local integration builds can set
`OrasSourceOverride` to an ORAS `src/OrasProject.Oras/` directory, including its
trailing slash.

Replace this project and its project references with the released
`OrasProject.Oras` package once both upstream changes are released and available
on the SDK's mirrored feeds. Remove the temporary sources and their manifest
entries at the same time.
