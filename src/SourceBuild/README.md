# Source-Build

This directory contains files necessary to generate a tarball that can be used
to build .NET from source.

For more information, see
[dotnet/source-build](https://github.com/dotnet/source-build).

## Local development workflow 

These are the steps used by some members of the .NET source-build team to create
a tarball and build it on a local machine as part of the development cycle:

1. Check out this repository and open a command line in the directory.
1. `./build.sh /p:ArcadeBuildTarball=true /p:TarballDir=/repos/tarball1 /p:PreserveTarballGitFolders=true`
    * The `TarballDir` can be anywhere you want outside of the repository.
1. `cd /repos/tarball1`
1. `./prep.sh`
1. `./build.sh --online`
1. Examine results and make changes to the source code in the tarball. The
   `.git` folders are preserved, so you can commit changes and save them as
   patches.
1. When a repo builds, source-build places a `.complete` file to prevent it from
   rebuilding again. This allows you to incrementally retry a build if there's a
   transient failure. But it also prevents you from rebuilding a repo after
   you've modified it.
    * To force a repo to rebuild with your new changes, run:  
      `rm -f ./artifacts/obj/semaphores/<repo>/Build.complete`
1. Run `./build.sh --online` again, and continue to repeat as necessary.

When developing a prebuilt removal change, examine the results of the build,
specifically:

* Prebuilt report. For example:  
  `./src/runtime.733a3089ec6945422caf06035c18ff700c9d51be/artifacts/source-build/self/prebuilt-report`

## Creating a patch file

To create a repo patch file, first commit your changes to the repo as normal,
then run this command inside the repo to generate a patch file inside the repo:

```sh
git format-patch --zero-commit --no-signature -1
```

Then, move the patch file into this repo, at
`src/SourceBuild/tarball/patches/<repo>`.

> If you define `PATCH_DIR` to point at the `patches` directory, you can use
> `-o` to place the patch file directly in the right directory:
>
> ```sh
> git format-patch --zero-commit --no-signature -1 -o "$PATCH_DIR/<repo>"
> ```

After generating the patch file, the numeric prefix on the filename may need to
be changed. By convention, new patches should be one number above the largest
number that already exists in the patch file directory. If there's a gap in the
number sequence, do not fix it (generally speaking), to avoid unnecessary diffs
and potential merge conflicts.

To apply a patch, or multiple patches, use `git am` while inside the target
repo. For example, to apply *all* `sdk` patches onto a fresh clone of the `sdk`
repository that has already been checked out to the correct commit, use:

```sh
git am "$PATCH_DIR/sdk/*"
```

This creates a Git commit with the patch contents, so you can easily amend a
patch or create a new commit on top that you can be sure will apply cleanly.

There is a method to create a series of patches based on a range of Git commits,
but this is not usually useful for 6.0 main development. It is used in servicing
to "freshen up" the sequence of patches (resolve conflicts) all at once.

Note: Tarballs have already applied patches to the source code.
