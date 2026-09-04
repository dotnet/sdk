---
coverage: Test-platform architecture and canonical ownership of local execution, authoring, CI selection, and Helix guidance
---

# Testing Strategy

All supported SDK test projects use MSTest.Sdk and Microsoft.Testing.Platform. Test
projects are generally grouped by product area under [`test/`](../../test); analyzer tests
are colocated under
[`src/Microsoft.CodeAnalysis.NetAnalyzers/tests`](../../src/Microsoft.CodeAnalysis.NetAnalyzers/tests).
[`test/TestAssets`](../../test/TestAssets) contains test inputs, not test projects.

## Canonical Sources

| Concern | Canonical source |
| --- | --- |
| Local test selection, source-to-project fallback mappings, product-layout freshness, and execution | [`run-tests` skill](../skills/run-tests/SKILL.md) |
| Test authoring, `SdkTest`, `SdkTestContext`, assets, parallelism, snapshots, and Helix-safe paths | [`test/AGENTS.md`](../../test/AGENTS.md) |
| Shared test harness | [`test/Microsoft.NET.TestFramework.MSTest`](../../test/Microsoft.NET.TestFramework.MSTest) |
| Configured PR test scopes and trigger paths | [`test/ConditionalTests.props`](../../test/ConditionalTests.props) |
| Conditional-filtering design and maintenance | [`pr-test-filtering.md`](../../documentation/project-docs/pr-test-filtering.md) |
| Full repository build and suite | [Root instructions](../../AGENTS.md#build-and-test) and [Developer Guide](../../documentation/project-docs/developer-guide.md#running-tests) |
| Helix publishing and partitioning | [`test/UnitTests.proj`](../../test/UnitTests.proj) |
| Reproducing a Helix layout locally | [`repro-helix-failure.md`](../../documentation/project-docs/repro-helix-failure.md) |

## Strategy

- Run the smallest project and filter that cover the changed behavior through
  `run-tests`; do not maintain a second source-to-project mapping.
- Keep the assembled SDK under test current before trusting product tests; the
  `run-tests` workflow owns that decision.
- Prefer reliable conditional PR scopes for substantive test areas. When safe selection
  is not possible, run tests rather than skip them.
- Reserve the full repository suite for broad validation; a whole-project local run is
  not equivalent to the complete suite.
- Treat the Helix filesystem as a deployed test layout rather than a repository checkout.
