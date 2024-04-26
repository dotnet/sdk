# Source-Build

This directory contains the .NET source build infrastructure.

_content_ - source build infrastructure mirrored to [dotnet/dotnet](https://github.com/dotnet/dotnet)
    [VMR](https://github.com/dotnet/arcade/blob/main/Documentation/UnifiedBuild/VMR-Design-And-Operation.md).

_patches_ - repo patches needed for .NET source build. Typically these are ephemeral to workaround integration
    issues. For more information, see the [Patch Guidelines](https://github.com/dotnet/source-build/blob/main/Documentation/patching-guidelines.md).

For more information, see [dotnet/source-build](https://github.com/dotnet/source-build).

## Local development workflow

When making changes to the source build infrastructure, devs would typically make and test the
changes in a local clone of [dotnet/dotnet](https://github.com/dotnet/dotnet). Once complete
you would copy the changed files here and make a PR. To validate the end to end experience, you
can synchronize the VMR with any changes made here by running [eng/vmr-sync.sh](https://github.com/dotnet/sdk/blob/main/eng/vmr-sync.sh).
