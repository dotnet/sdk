# SDK Build Duty Triage Report

**Generated:** January 26, 2026  
**Repos Monitored:** dotnet/sdk, dotnet/templating, dotnet/dotnet (VMR - SDK-owned only)

---

## Summary

| Category | Count |
|----------|-------|
| 🟢 Ready to Merge | 1 |
| ⏳ Waiting/On Hold | 1 |
| 🔴 Failing/Blocked | 13 |
| 🟡 Branch Lockdown | 10 |
| 🟠 New/Pending Validation | 2 |

---

## 🟢 Ready to Merge PRs (1)

These PRs have all checks passing and are ready for merge.

### dotnet/dotnet VMR

| PR | Title | Target Branch | Age |
|----|-------|---------------|-----|
| [#4419](https://github.com/dotnet/dotnet/pull/4419) | [release/10.0.1xx] Source code updates from dotnet/source-build-reference-packages | release/10.0.1xx | 0d |

---

## 🟠 New/Pending Validation PRs (2)

These PRs were just created and are awaiting initial CI validation.

### dotnet/sdk

| PR | Title | Target Branch | Age |
|----|-------|---------------|-----|
| [#52673](https://github.com/dotnet/sdk/pull/52673) | [release/10.0.1xx] Source code updates from dotnet/dotnet | release/10.0.1xx | 0d |

### dotnet/templating

| PR | Title | Target Branch | Age |
|----|-------|---------------|-----|
| [#9757](https://github.com/dotnet/templating/pull/9757) | [release/10.0.1xx] Source code updates from dotnet/dotnet | release/10.0.1xx | 3d |

---

## ⏳ Waiting/On Hold PRs (1)

These PRs have passing checks but have comments indicating they should wait before merging.

### dotnet/templating

| PR | Title | Target Branch | Age | Reason |
|----|-------|-----------------|-----|--------|
| [#9754](https://github.com/dotnet/templating/pull/9754) | [release/10.0.3xx] Source code updates from dotnet/dotnet | release/10.0.3xx | 4d | **Will break build** - Arcade version flow issue. Waiting for Arcade 10 to flow through VMR. CC @MichaelSimons. Also linked to [Issue #51574](https://github.com/dotnet/sdk/issues/51574). |

---

## 🟡 Branch Lockdown PRs (10)

These PRs are in branches with lockdown labels and require approval to merge.

### dotnet/sdk (8)

| PR | Title | Target Branch | Age |
|----|-------|---------------|-----|
| [#52667](https://github.com/dotnet/sdk/pull/52667) | [release/9.0.1xx] Update dependencies from dotnet/roslyn-analyzers | release/9.0.1xx | 1d |
| [#52624](https://github.com/dotnet/sdk/pull/52624) | [release/9.0.3xx] Update dependencies from dotnet/scenario-tests | release/9.0.3xx | 4d |
| [#52606](https://github.com/dotnet/sdk/pull/52606) | [release/9.0.1xx] Update dependencies from dotnet/scenario-tests | release/9.0.1xx | 5d |
| [#52594](https://github.com/dotnet/sdk/pull/52594) | [release/9.0.3xx] Update dependencies from dotnet/msbuild | release/9.0.3xx | 5d |
| [#52592](https://github.com/dotnet/sdk/pull/52592) | [release/9.0.3xx] Update dependencies from dotnet/arcade | release/9.0.3xx | 5d |
| [#52591](https://github.com/dotnet/sdk/pull/52591) | [release/9.0.1xx] Update dependencies from dotnet/source-build-reference-packages | release/9.0.1xx | 5d |
| [#52590](https://github.com/dotnet/sdk/pull/52590) | [release/9.0.1xx] Update dependencies from dotnet/arcade | release/9.0.1xx | 5d |
| [#52530](https://github.com/dotnet/sdk/pull/52530) | Merge branch 'release/8.0.1xx' => 'release/8.0.4xx' | release/8.0.4xx | 8d |

### dotnet/templating (2)

| PR | Title | Target Branch | Age |
|----|-------|---------------|-----|
| [#9746](https://github.com/dotnet/templating/pull/9746) | [release/9.0.1xx] Update dependencies from dotnet/arcade | release/9.0.1xx | 5d |
| [#9744](https://github.com/dotnet/templating/pull/9744) | [release/9.0.3xx] Update dependencies from dotnet/arcade | release/9.0.3xx | 5d |

---

## 🔴 Failing/Blocked PRs (13)

PRs with pending or failing status checks.

### dotnet/sdk (11)

| PR | Title | Target Branch | Age | Issue |
|----|-------|---------------|-----|-------|
| [#52662](https://github.com/dotnet/sdk/pull/52662) | [release/10.0.2xx] Source code updates from dotnet/dotnet | release/10.0.2xx | 3d | ⚠️ Opposite codeflow merged - needs decision (merge/close/force) |
| [#52657](https://github.com/dotnet/sdk/pull/52657) | Merge branch 'release/10.0.1xx' => 'release/10.0.2xx' | release/10.0.2xx | 3d | ⏳ Checks pending |
| [#52653](https://github.com/dotnet/sdk/pull/52653) | [release/10.0.2xx] Update dependencies from microsoft/testfx | release/10.0.2xx | 3d | ⏳ Checks pending |
| [#52652](https://github.com/dotnet/sdk/pull/52652) | [release/10.0.1xx] Update dependencies from microsoft/testfx | release/10.0.1xx | 3d | ⏳ Checks pending |
| [#52651](https://github.com/dotnet/sdk/pull/52651) | [main] Update dependencies from microsoft/testfx | main | 3d | ⏳ Checks pending |
| [#52596](https://github.com/dotnet/sdk/pull/52596) | [main] Source code updates from dotnet/dotnet | main | 5d | ❌ ILLink analyzer error: `System.MissingMethodException` - [Issue #52599](https://github.com/dotnet/sdk/issues/52599) |
| [#52588](https://github.com/dotnet/sdk/pull/52588) | Merge branch 'release/10.0.2xx' => 'release/10.0.3xx' | release/10.0.3xx | 5d | ❌ Build error: `TagHelperCollection` not found in RazorSdk - @dotnet/razor-tooling investigating |
| [#52585](https://github.com/dotnet/sdk/pull/52585) | [release/10.0.3xx] Source code updates from dotnet/dotnet | release/10.0.3xx | 5d | ⏳ Checks pending |
| [#52523](https://github.com/dotnet/sdk/pull/52523) | [release/10.0.2xx] Source code updates from dotnet/dotnet | release/10.0.2xx | 9d | ❌ Restore error NU1603: `Microsoft.Deployment.DotNet.Releases` version mismatch |
| [#52519](https://github.com/dotnet/sdk/pull/52519) | Merge branch 'release/10.0.3xx' => 'main' | main | 9d | ❌ Test failures: `XunitMultiTFM`, `RunWithSolutionFilterAsFirstUnmatchedToken` - fix pushed, awaiting green |

### github-actions[bot] Merge PRs (2)

*Note: These are cross-branch merge PRs that require manual attention.*

| Repo | PR | Title | Target Branch | Age | Issue |
|------|-----|-------|---------------|-----|-------|
| dotnet/sdk | [#52530](https://github.com/dotnet/sdk/pull/52530) | Merge 'release/8.0.1xx' => 'release/8.0.4xx' | release/8.0.4xx | 8d | Branch Lockdown |
| dotnet/sdk | [#52529](https://github.com/dotnet/sdk/pull/52529) | Merge 'release/8.0.4xx' => 'release/9.0.1xx' | release/9.0.1xx | 8d | Branch Lockdown |

---

## Notes

- **Status API shows pending for all PRs** - These repos use Azure Pipelines/GitHub Checks API which doesn't populate the Status API. Actual check status determined from comments and PR state.
- All PRs checked against authors: `dotnet-maestro[bot]`, `github-actions[bot]` (Merge PRs only)
- dotnet/dotnet VMR PRs filtered to SDK-owned: `dotnet/sdk`, `dotnet/templating`, `dotnet/deployment-tools`, `dotnet/source-build-reference-packages`

### Total PR Count by Repo

| Repo | Count |
|------|-------|
| dotnet/sdk | 19 |
| dotnet/templating | 4 |
| dotnet/dotnet (VMR) | 1 |
| **Total** | **24** |

### Key Issues Blocking Multiple PRs

| Issue | Affected PRs | Status |
|-------|--------------|--------|
| [#52599](https://github.com/dotnet/sdk/issues/52599) - ILLink analyzer MissingMethodException | #52596 | Open, @MiYanni investigating |
| [#51574](https://github.com/dotnet/sdk/issues/51574) - Arcade version flow | templating #9754 | On hold |
| RazorSdk TagHelperCollection missing | #52588 | @dotnet/razor-tooling investigating |
| Opposite codeflow merged | #52662, #52596, #52523 | Needs decision: merge/close/force |
| Branch Lockdown (9.0.x branches) | 8 SDK PRs, 2 templating PRs | Requires approval |

---

*Report generated by SDK Build Duty skill*
