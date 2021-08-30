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
Note that this will install dotnet format to an isolated environment, using the system installation of the dotnet CLI. See the [pre-commit.com documentation](https://pre-commit.com/#dotnet) for more details.

These instructions originally authored by [rkm](https://github.com/rkm)
