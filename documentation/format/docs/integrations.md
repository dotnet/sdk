# Integrations
Collection of advice how to auto check/format. Every sample expects dotnet format installed as local tool, unless otherwise noted.

## Git pre-commit hook to reformat

Create file `.git/hooks/pre-commit` with following contents:
```sh
#!/bin/sh
LC_ALL=C
# Select files to format
FILES=$(git diff --cached --name-only --diff-filter=ACM "*.cs" | sed 's| |\\ |g')
[ -z "$FILES" ] && exit 0

# Format all selected files
echo "$FILES" | cat | xargs | sed -e 's/ /,/g' | xargs dotnet format --include

# Add back the modified files to staging
echo "$FILES" | xargs git add

exit 0

```

These instructions originally authored by [randulakoralage82](https://medium.com/@randulakoralage82/format-your-net-code-with-git-hooks-a0dc33f68048).


## Check on PR in Azure Dev Ops

Add following to your build file:

```yaml
- task: UseDotNet@2
  displayName: 'Use .NET 6 sdk'
  inputs:
    packageType: 'sdk'
    version: '6.0.x'
    includePreviewVersions: true

- task: DotNetCoreCLI@2
  displayName: 'dotnet-format'
  inputs:
    command: 'custom'
    custom: 'format'
    arguments: '--verify-no-changes'
```

These instructions originally authored by [leotsarev](https://github.com/joinrpg/joinrpg-net/).


## [pre-commit.com](https://pre-commit.com/) hook to reformat

Add the following block to the `repos` section of your `.pre-commit-config.yaml` file:

```yaml
-   repo: https://github.com/dotnet/format
    rev: ""  # Specify a tag or sha here, or run "pre-commit autoupdate"
    hooks:
    -   id: dotnet-format
```
Note that this will compile and install dotnet format to an isolated environment, using the system installation of the dotnet CLI. See the [pre-commit.com documentation](https://pre-commit.com/#dotnet) for more details. The problem is that dotnet format is using *preview* SDK (even for 5.x versions), and you have to install preview SDK on your machine for compiling it. Another option is to use local feature of pre-commit, as follows:

```yaml
-   repo: local
    hooks:
    #Use dotnet format already installed on your machine
    -   id: dotnet-format
        name: dotnet-format
        language: system 
        entry: dotnet format --include 
        types_or: ["c#", "vb"]
```

These instructions originally authored by [rkm](https://github.com/rkm) & [leotsarev](https://github.com/joinrpg/joinrpg-net/).


## Rider reformat on save

1. Open Settings -> Tools -> File Watchers
1. Press The “Plus Sign” to Add a Custom watcher
1. Set the name to i.e. “dotnet format on save”
1. FileType: C#
1. Scope: Open Files
1. Program: Write dotnet-format
1. Arguments: $SolutionPath$ --verbosity diagnostic --include $FileRelativePath$
1. (Optionally) Append --fix-style warning to fix any style issues automatically on save.
1. (Optionally) Append --fix-analyzers warning to fix any analyzer warnings on save.
1. Disable all advanced option checkboxes.
1. All other values were left default

These instructions originally authored by [Nils Henrik Hals](https://strepto.github.io/Pause/blog/dotnet-format-rider/).
