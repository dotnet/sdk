# Instructions for Roslyn developers

1. Check out a working copy of dotnet/roslyn, e.g. to the directory next to this repo's root `..\roslyn`.
   Make sure the current working directory is the checkout of this repo.

2. Copy LocalDev.Build.props to the root and set `RoslynRoot` property in the copied file to local Roslyn repo.

    `cp documentation\local-dev\roslyn\LocalDev.Build.props LocalDev.Build.props`

3. Copy LocalDev.Packages.props.roslyn to the root

    `cp documentation\local-dev\roslyn\LocalDev.Packages.props LocalDev.Packages.props`

4. Build Roslyn with:

    `..\roslyn\Build.cmd -restore -pack`

5. Clear the customized packages cache in `.packages`

    `git clean -dxf .packages/`

6. Restore packages

7. Open solution and work normally

8. Following another change to Roslyn.sln, repeat steps 4-6 to pick up those changes in sdk.

Tip to speed up restore if .\packages starts containing more than just the Roslyn development packages. Clear the
packages cache, and restore packages without using the customized local developer settings to update the per-user
NuGet global package cache with the default packages used for Conversations. Then run a second restore to pick up
just the local development packages for Roslyn.

```
git clean -dxf .packages/
restore /p:SkipLocalDevSettings=true
restore
```