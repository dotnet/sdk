# Source-Build

This directory contains the .NET source build infrastructure.

_content_ - source build infrastructure mirrored to [dotnet/dotnet](https://github.com/dotnet/dotnet)
    [VMR](https://github.com/dotnet/arcade/blob/main/Documentation/UnifiedBuild/VMR-Design-And-Operation.md).

_patches_ - repo patches needed for .NET source build. Typically these are ephemeral to workaround integration
    issues. Patches should always have a tracking issue/pr to backport the fix or address the underlying issue
    being worked around.

For more information, see [dotnet/source-build](https://github.com/dotnet/source-build).

## Local development workflow

When making changes to the source build infrastructure, devs would typically make and test the
changes in a local clone of [dotnet/dotnet](https://github.com/dotnet/dotnet). Once complete
you would copy the changed files here and make a PR. To validate the end to end experience, you
can synchronize the VMR with any changes made here by running [eng/vmr-sync.sh](https://github.com/dotnet/installer/blob/main/eng/vmr-sync.sh).

## Creating a patch file

To create a repo patch file, first commit your changes to the repo as normal,
then run this command inside the repo to generate a patch file inside the repo:

```sh
git format-patch --zero-commit --no-signature -1
```

Then, move the patch file into this repo, at
`src/SourceBuild/patches/<repo>`.

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

> Note: The VMR has all of the `src/SourceBuild/patches` applied. This is done as part of the
[synchronization process](https://github.com/dotnet/arcade/blob/main/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#source-build-patches).
