# Runtime Version Update Plan

This document describes the process for updating the runtime version in the SDK repository to a specific version (e.g., v10.0.102) using DARC commands.

## Overview

When updating the runtime version, we need to:
1. Update dependencies from the dotnet/dotnet repository to the target version
2. Preserve the versions of non-dotnet-dotnet dependencies by running follow-up update-dependencies commands

The first `update-dependencies` command for the target version (e.g., v10.0.102) will incorrectly update **all** dependency versions. The follow-up commands restore the correct versions for non-dotnet-dotnet dependencies.

---

## Step-by-Step Process

### Step 1: Get the commit SHA for the target version tag

Fetch the commit SHA from the dotnet/dotnet repository for the target tag (e.g., `v10.0.102`):

```bash
# Using git to get the commit for a tag
git ls-remote https://github.com/dotnet/dotnet refs/tags/v10.0.102

# Or if you have the repo cloned:
git rev-parse v10.0.102^{commit}
```

**Output:** A commit SHA (e.g., `abc123def456...`)

---

### Step 2: Get the BarID for the dotnet/dotnet build

Use the DARC CLI to get the build information:

```bash
darc get-build --repo https://github.com/dotnet/dotnet --commit <commit-sha-from-step-1>
```

**Output:** Build information including a `BarId` (e.g., `291289`)

---

### Step 3: Record non-dotnet-dotnet dependencies

Before running the first update-dependencies, record all unique repo/commit combinations from `eng/Version.Details.xml` that do NOT come from `dotnet-dotnet` (Azure DevOps) or `github.com/dotnet/dotnet`.

**Dependencies to record (based on current Version.Details.xml):**

| Repository | Commit SHA |
|------------|------------|
| `https://github.com/dotnet/templating` | `069bda6132d6ac2134cc9b26d651ccb825ff212d` |
| `https://github.com/dotnet/core-setup` | `7d57652f33493fa022125b7f63aad0d70c52d810` |
| `https://github.com/dotnet/msbuild` | `2960e90f194e80f8f664ac573d456058bc4f5cd9` |
| `https://github.com/dotnet/fsharp` | `89d788641914c5d0b87fddfa11f4df0b5cfaa73d` |
| `https://github.com/dotnet/roslyn` | `46a48b8c1dfce7c35da115308bedd6a5954fd78a` |
| `https://github.com/nuget/nuget.client` | `b5efdd1f17df11700c9383def6ece79a40218ccd` |
| `https://github.com/microsoft/vstest` | `bbee830b0ef18eb5b4aa5daee65ae35a34f8c132` |

**Note:** Dependencies from these sources should be **excluded** (they come from dotnet-dotnet VMR):
- `https://dev.azure.com/dnceng/internal/_git/dotnet-dotnet`
- `https://github.com/dotnet/dotnet`

#### Script to extract non-dotnet-dotnet dependencies:

```powershell
# PowerShell script to extract unique repo/commit pairs
[xml]$xml = Get-Content "eng/Version.Details.xml"
$deps = $xml.Dependencies.ProductDependencies.Dependency + $xml.Dependencies.ToolsetDependencies.Dependency

$nonVmrDeps = $deps | Where-Object { 
    $_.Uri -and 
    $_.Uri -notmatch "dotnet-dotnet" -and 
    $_.Uri -notmatch "github\.com/dotnet/dotnet"
} | ForEach-Object {
    [PSCustomObject]@{
        Uri = $_.Uri
        Sha = $_.Sha
    }
} | Sort-Object Uri, Sha -Unique

$nonVmrDeps | Format-Table -AutoSize
```

```bash
# Bash alternative
grep -E "<Uri>|<Sha>" eng/Version.Details.xml | \
  grep -v "dotnet-dotnet" | grep -v "github.com/dotnet/dotnet" | \
  paste - - | sort -u
```

---

### Step 4: Run update-dependencies for the target version

```bash
darc update-dependencies --id <bar-id-from-step-2>
```

This will update ALL dependencies to the versions from the v10.0.102 VMR build, **including incorrectly updating non-VMR dependencies**.

---

### Step 5: Get BarIDs for each non-dotnet-dotnet dependency

For each unique repo/commit pair recorded in Step 3, get its BarID:

```bash
# For each repo/commit pair:
darc get-build --repo <repo-uri> --commit <commit-sha>
```

**Example commands:**
```bash
darc get-build --repo https://github.com/dotnet/templating --commit 069bda6132d6ac2134cc9b26d651ccb825ff212d
darc get-build --repo https://github.com/dotnet/msbuild --commit 2960e90f194e80f8f664ac573d456058bc4f5cd9
darc get-build --repo https://github.com/dotnet/fsharp --commit 89d788641914c5d0b87fddfa11f4df0b5cfaa73d
darc get-build --repo https://github.com/dotnet/roslyn --commit 46a48b8c1dfce7c35da115308bedd6a5954fd78a
darc get-build --repo https://github.com/nuget/nuget.client --commit b5efdd1f17df11700c9383def6ece79a40218ccd
darc get-build --repo https://github.com/microsoft/vstest --commit bbee830b0ef18eb5b4aa5daee65ae35a34f8c132
```

---

### Step 6: Run update-dependencies for each non-dotnet-dotnet BarID

For each BarID obtained in Step 5, run update-dependencies to restore the correct versions:

```bash
darc update-dependencies --id <bar-id-for-templating>
darc update-dependencies --id <bar-id-for-msbuild>
darc update-dependencies --id <bar-id-for-fsharp>
darc update-dependencies --id <bar-id-for-roslyn>
darc update-dependencies --id <bar-id-for-nuget>
darc update-dependencies --id <bar-id-for-vstest>
# ... repeat for each repo
```

---

### Step 7: Verify the changes

After all update-dependencies commands complete:

1. Review the diff in `eng/Version.Details.xml`
2. Verify that dotnet-dotnet dependencies point to the new v10.0.102 commit
3. Verify that non-dotnet-dotnet dependencies remain at their original versions
4. Run a build to ensure everything compiles

---

## Automation Script Template

```bash
#!/bin/bash
set -e

TARGET_TAG="v10.0.102"

# Step 1: Get commit for target tag
TARGET_COMMIT=$(git ls-remote https://github.com/dotnet/dotnet "refs/tags/${TARGET_TAG}" | cut -f1)
echo "Target commit: $TARGET_COMMIT"

# Step 2: Get BarID for target
TARGET_BAR_ID=$(darc get-build --repo https://github.com/dotnet/dotnet --commit "$TARGET_COMMIT" --output-format json | jq -r '.id')
echo "Target BarID: $TARGET_BAR_ID"

# Step 3: Record non-dotnet-dotnet dependencies (manual step - see table above)
# Store as array of "repo|commit" pairs
declare -a NON_VMR_DEPS=(
    "https://github.com/dotnet/templating|069bda6132d6ac2134cc9b26d651ccb825ff212d"
    "https://github.com/dotnet/msbuild|2960e90f194e80f8f664ac573d456058bc4f5cd9"
    "https://github.com/dotnet/fsharp|89d788641914c5d0b87fddfa11f4df0b5cfaa73d"
    "https://github.com/dotnet/roslyn|46a48b8c1dfce7c35da115308bedd6a5954fd78a"
    "https://github.com/nuget/nuget.client|b5efdd1f17df11700c9383def6ece79a40218ccd"
    "https://github.com/microsoft/vstest|bbee830b0ef18eb5b4aa5daee65ae35a34f8c132"
)

# Step 4: Run initial update-dependencies
echo "Running update-dependencies for target version..."
darc update-dependencies --id "$TARGET_BAR_ID"

# Steps 5 & 6: Get BarIDs and run update-dependencies for each non-VMR dep
for dep in "${NON_VMR_DEPS[@]}"; do
    IFS='|' read -r repo commit <<< "$dep"
    echo "Processing: $repo @ $commit"
    
    BAR_ID=$(darc get-build --repo "$repo" --commit "$commit" --output-format json | jq -r '.id')
    if [ -n "$BAR_ID" ] && [ "$BAR_ID" != "null" ]; then
        echo "  BarID: $BAR_ID"
        darc update-dependencies --id "$BAR_ID"
    else
        echo "  WARNING: Could not find build for $repo @ $commit"
    fi
done

echo "Done! Review changes in eng/Version.Details.xml"
```

---

## Notes

- **Pinned dependencies** (like `NETStandard.Library.Ref`) should not be updated and will remain unchanged.
- The `dotnet/core-setup` dependency for `NETStandard.Library.Ref` is very old and pinned; it may not have a retrievable BarID.
- Some dependencies may share the same commit SHA if they're built from the same repo at the same time.
- Always verify the final state matches expectations before committing.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `darc get-build` returns no results | The build may not be registered in BAR. Check if the commit is correct. |
| Dependencies still wrong after updates | Re-run the specific update-dependencies for that repo's BarID. |
| Version mismatch errors during build | Some packages may have interdependencies. Check version coherence. |
