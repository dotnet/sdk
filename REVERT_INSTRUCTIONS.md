# Revert Instructions for Commit 7bd19ab8af

## Problem
Commit `7bd19ab8afb074710ba146d82466d7c92d5baa78` in PR #52007 added defensive error handling to `eng/common/tools.ps1` that needs to be reverted.

## Solution Created
A proper revert commit has been created locally:
- **Commit Hash**: `368e8b3a5ef4cb6ddf743093190eac2327354702`
- **Parent**: `7bd19ab8afb074710ba146d82466d7c92d5baa78` (the commit being reverted)
- **Message**: "Revert 'Revert change to tools.ps1'"

## Changes Reverted
The revert removes the following changes from 7bd19ab8af:
1. Error handling for `$GlobalJson.tools.vs` - changed from:
   ```powershell
   if (!$vsRequirements) {
     if (Get-Member -InputObject $GlobalJson.tools -Name 'vs' -ErrorAction SilentlyContinue) {
       $vsRequirements = $GlobalJson.tools.vs
     } else {
       $vsRequirements = $null
     }
   }
   ```
   Back to: `if (!$vsRequirements) { $vsRequirements = $GlobalJson.tools.vs }`

2. Null checks on `$vsRequirements` member access - removed `-ErrorAction SilentlyContinue` and null check from:
   - `if ($vsRequirements -and (Get-Member -InputObject $vsRequirements -Name 'version' -ErrorAction SilentlyContinue))`
   - `if ($vsRequirements -and (Get-Member -InputObject $vsRequirements -Name 'components' -ErrorAction SilentlyContinue))`

3. Whitespace - restored trailing whitespace on line 298
4. End of file - restored newline character

## How to Apply
Since PR #52007 is based on `darc-release/9.0.3xx` branch and this PR branch is based on `main`, the revert commit needs to be applied directly to PR #52007's branch or cherry-picked.

### Manual Application
Apply these changes to `eng/common/tools.ps1` on PR #52007's branch:
1. Line 298: Add two trailing spaces after the `$runtimePath` assignment
2. Lines 550-556: Replace the multi-line error handling with: `if (!$vsRequirements) { $vsRequirements = $GlobalJson.tools.vs }`
3. Line 557 (previously 563): Remove `$vsRequirements &&` and `-ErrorAction SilentlyContinue` from the version check
4. Line 562 (previously 568): Remove `$vsRequirements &&` and `-ErrorAction SilentlyContinue` from the components check  
5. End of file: Ensure there's a newline after the closing `}`

## Verification
After applying, verify with:
```bash
git diff 7bd19ab8af^..HEAD -- eng/common/tools.ps1
```
Should show no differences, confirming the file matches the state before 7bd19ab8af.
