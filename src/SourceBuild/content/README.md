# dotnet/dotnet - Home of the .NET VMR

This repository is a **Virtual Monolithic Repository (VMR)** which includes all the source code and infrastructure needed to build the .NET SDK.

What this means:

- **Monolithic** - a join of multiple repositories that make up the whole product, such as [dotnet/runtime](https://github.com/dotnet/runtime) or [dotnet/sdk](https://github.com/dotnet/sdk).
- **Virtual** - a mirror (not replacement) of product repos where sources from those repositories are synchronized into.

In the VMR, you can find:

- source files of each product repository which are mirrored inside of their respective directories under [`src/`](https://github.com/dotnet/dotnet/tree/main/src),
- tooling that enables [building the whole .NET product from source](https://github.com/dotnet/source-build) on Linux platforms,
- small customizations, in the form of [patches](https://github.com/dotnet/dotnet/tree/main/src/sdk/src/SourceBuild/patches), applied on top of the original code to make the build possible,
- *[in future]* E2E tests for the whole .NET product.

Just like the development repositories, the VMR will have a release branch for every feature band (e.g. `release/8.0.1xx`).
Similarly, VMR's `main` branch will follow `main` branches of product repositories (see [Synchronization Based on Declared Dependencies](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#synchronization-based-on-declared-dependencies)).

More in-depth documentation about the VMR can be found in [VMR Design And Operation](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#layout).
See also [dotnet/source-build](https://github.com/dotnet/source-build) for more information about our whole-product source-build.

## Goals

- The main purpose of the [dotnet/dotnet](https://github.com/dotnet/dotnet) repository is to have all source code necessary to build the .NET product available in one repository and identified by a single commit.
- The VMR also aims to become the place from which we release and service future versions of .NET to reduce the complexity of the product construction process. This should allow our partners and and 3rd parties to easily build, test and modify .NET using their custom infrastructure as well as make the process available to the community.
- Lastly, we hope to solve other problems that the current multi-repo setup brings:
  - Enable the standard [down-/up-stream open-source model](src/arcade/Documentation/UnifiedBuild/VMR-Upstream-Downstream.md).
  - Fulfill requirements of .NET distro builders such as RedHat or Canonical to natively include .NET in their distribution repositories.
  - Simplify scenarios such as client-run testing of bug fixes and improvements. The build should work in an offline environment too for certain platforms.
  - Enable developers to make and test changes spanning multiple repositories.
  - More efficient pipeline for security fixes during the CVE pre-disclosure process.

We will achieve these goals while keeping active coding work in the separate repos where it happens today. For example: ASP.NET features will continue to be developed in `dotnet/aspnetcore` and CLR features will be continue to be developed in `dotnet/runtime`. Each of these repos have their own distinct communities and processes, and aggregating development into a true mono-repo would work against that. Hence, the "virtual" monolithic repo: the VMR gives us the simplicity of a mono-repo for building and servicing the product, while active development of components of that product stays in its various existing repos. The day to day experience for typical contributors will not change.

## Limitations

**This is a work-in-progress.**
There are considerable limitations to what is possible at the moment. For an extensive list of current limitations, please see [Temporary Mechanics](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#temporary-mechanics).  
See the [Unified Build roadmap](src/arcade/Documentation/UnifiedBuild/Roadmap.md) for more details.

### Supported platforms

- 8.0 and 9.0
  - source-build configuration on Linux
- 10.0+ (WIP)
  - source-build configuration on Linux
  - non-source-build configuration on Linux, Mac, and Windows

For the latest information about Source-Build support for new .NET versions, please check our [GitHub Discussions page](https://github.com/dotnet/source-build/discussions) for announcements.

### Code flow

For the time being, the source code only flows one way - from the development repos into the VMR.
More details on this process:

- [Source Synchronization Process](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#source-synchronization-process)
- [Synchronization Based on Declared Dependencies](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#synchronization-based-on-declared-dependencies)
- [Moving Code and Dependencies between the VMR and Development Repos](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#moving-code-and-dependencies-between-the-vmr-and-development-repos)

We expect the code flow to start working both ways at the completion of the Unified Build project.
See the [Unified Build roadmap](src/arcade/Documentation/UnifiedBuild/Roadmap.md) for more details.

### Contribution

At this time, the VMR will not accept any changes and is a read-only mirror of the development repositories only.
Please, make the changes in the respective development repositories (e.g., [dotnet/runtime](https://github.com/dotnet/runtime) or [dotnet/sdk](https://github.com/dotnet/sdk)) and they will get synchronized into the VMR automatically.

## Dev instructions

Please note that **this repository is a work-in-progress** and there are some usability issues connected to this.
These can be nuisances such as some checked-in files getting modified by the build itself and similar.
For the latest information about Source-Build support, please watch for announcements posted on our [GitHub Discussions page](https://github.com/dotnet/source-build/discussions).

### Prerequisites

The dependencies for building can be found [here](https://github.com/dotnet/runtime/blob/main/docs/workflow/requirements/).
In case you don't want to / cannot prepare your environment per the requirements, consider [using Docker](#building-using-docker).

For building the VMR on Windows, it is recommended to put the repo under a short path, i.e. `C:\dotnet`. Also, [long path support must be enabled](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=registry#enable-long-paths-in-windows-10-version-1607-and-later). This is necessary as some of the tools used don't support long paths (WiX Toolset v3 and cl.exe).

For some `git` commands and when synchronizing changes via the `darc` CLI, long path support should be enabled in the `git` config as well:
```bash
git config --system core.longpaths true # needs elevated prompt
git config --global core.longpaths true
```

### Building

1. **Clone the repository**

   ```bash
   git clone https://github.com/dotnet/dotnet dotnet-dotnet
   cd dotnet-dotnet
   ```

1. **Build the .NET SDK**

    Choose one of the following build modes:

    - **Microsoft based build**

        For Unix:

        ```bash
        ./build.sh --clean-while-building
        ```

        For Windows:

        ```cmd
        .\build.cmd -cleanWhileBuilding
        ```

    - **Building from source**

        ```bash
        # Prep the source to build on your distro.
        # This downloads a .NET SDK and a number of .NET packages needed to build .NET from source.
        ./prep-source-build.sh

        # Build the .NET SDK
        ./build.sh -sb --clean-while-building
        ```

    The resulting SDK is placed at `artifacts/assets/Release/dotnet-sdk-9.0.100-[your-RID].tar.gz` (for Unix) or `artifacts/assets/Release/dotnet-sdk-9.0.100-[your-RID].zip` (for Windows).

1. *(Optional)* **Unpack and install the .NET SDK**

    For Unix:

    ```bash
    mkdir -p $HOME/dotnet
    tar zxf artifacts/assets/Release/dotnet-sdk-9.0.100-[your-RID].tar.gz -C $HOME/dotnet
    ln -s $HOME/dotnet/dotnet /usr/bin/dotnet
    ```

    For Windows:

    ```cmd
    mkdir %userprofile%\dotnet
    tar -xf artifacts/assets/Release/dotnet-sdk-9.0.100-[your RID].zip -C %userprofile%\dotnet
    set "PATH=%userprofile%\dotnet;%PATH%"
    ```

    To test your built SDK, run the following:

    ```bash
    dotnet --info
    ```

> [!NOTE]
> Run `./build.sh --help` (for Unix) or `.\build.cmd -help` (for Windows) to see more information about supported build options.

### Building using Docker

You can also build the repository using a Docker image which has the required prerequisites inside.
The example below creates a Docker volume named `vmr` and clones and builds the VMR there.

```sh
docker run --rm -it -v vmr:/vmr -w /vmr mcr.microsoft.com/dotnet-buildtools/prereqs:centos-stream9
git clone https://github.com/dotnet/dotnet .

# - Microsoft based build
./build.sh --clean-while-building

# - Building from source
./prep-source-build.sh && ./build.sh -sb --clean-while-building

mkdir -p $HOME/.dotnet
tar -zxf artifacts/assets/Release/dotnet-sdk-9.0.100-centos.9-x64.tar.gz -C $HOME/.dotnet
ln -s $HOME/.dotnet/dotnet /usr/bin/dotnet
```

### Codespaces

You can also utilize [GitHub Codespaces](https://github.com/features/codespaces) where you can find preset containers in this repository.

### Building from released sources

You can also build from sources (and not from a context of a git repository), such as the ones you can acquire from a [dotnet/dotnet release](https://github.com/dotnet/dotnet/releases).
In this case, you need to provide additional information which includes the original repository and commit hash the code was built from so that the SDK can provide a better debugging experience (think the `Step into..` functionality).
Usually, this means the [dotnet/dotnet repository](https://github.com/dotnet/dotnet) together with the commit the release tag is connected to.

In practice, this means that when calling the main build script, you need to provide additional arguments when building outside of a context of a git repository.  
Alternatively, you can also provide a manifest file where this information can be read from. This file (`release.json`) can be found attached with the [dotnet/dotnet release](https://github.com/dotnet/dotnet/releases).

### Synchronizing code into the VMR

Sometimes you want to make a change in a repository and test that change in the VMR. You could of course make the change in the VMR directly (locally, as the VMR is read-only for now) but in case it's already available in your repository, you can synchronize it into the VMR (again locally).

To do this, you can either start a [dotnet/dotnet](https://github.com/dotnet/dotnet) Codespace - you will see instructions right after it starts. Alternatively, you can clone the repository locally and use the [vmr-sync.sh](src/sdk/eng/vmr-sync.sh) or [vmr-sync.ps1](src/sdk/eng/vmr-sync.ps1) script to pull your changes in. Please refer to the documentation in the script for more details.

## Filing Issues

This repo does not accept issues as of now. Please file issues to the appropriate development repos.
For issues with the VMR itself, please use the [source-build repository](https://github.com/dotnet/source-build).

## Useful Links

- Design documentation for the VMR - a set of documents describing the high-level design and the why's and how's
  - [Design and Operation](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md)
  - [Upstream/Downstream Relationships](src/arcade/Documentation/UnifiedBuild/VMR-Upstream-Downstream.md)
  - [Code and Build Workflow](src/arcade/Documentation/UnifiedBuild/VMR-Code-And-Build-Workflow.md)
  - [Strategy for Managing External Source Dependencies](src/arcade/Documentation/UnifiedBuild/VMR-Strategy-For-External-Source.md)
- [.NET Source-Build](https://github.com/dotnet/source-build)
- [What is .NET](https://dotnet.microsoft.com)

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

## License

.NET is licensed under the [MIT](LICENSE.TXT) license.
