# Integrations
Collection of advice how to auto check/format. Every sample expects dotnet format installed as local tool.

# Pre-commit hook to reformat

Create file `.git/pre-commit` with following contents 
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


# Check on PR in Azure Dev Ops

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Install dotnet tools'
  inputs:
    command: 'custom'
    custom: 'tool'
    arguments: 'restore'

- task: DotNetCoreCLI@2
  displayName: 'dotnet-format'
  inputs:
    command: 'custom'
    custom: 'format'
    arguments: '--check'
```
