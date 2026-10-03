---
name: roslyn-analyzers
description: >-
   Design, implement, test, package, or adopt a Roslyn diagnostic analyzer, code fix, or
   suppressor. Use for DiagnosticAnalyzer, CodeFixProvider, DiagnosticSuppressor,
   analyzer-package, Fix All, analyzer-performance, and analyzer-enablement work.
license: MIT
compatibility: Requires the .NET SDK, Roslyn packages, and a test project capable of running Microsoft.CodeAnalysis.Testing fixtures.
metadata:
  portability: portable
  applicability: dotnet-project-gated
  binding: optional-overlay
  risk: local-write
  maturity: canary
  requires: none
  related: none
---

# Roslyn analyzers

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Scale the workflow to the task. Investigation and prototyping can start with a focused
repro; shipping or broadly enabling a diagnostic requires the full validation and
performance checks below.

## Workflow

1. **Read repository bindings.** Follow the existing project, ID, release, packaging,
   and test conventions. Inspect neighboring analyzers for integration details, but
   validate their design against this skill rather than copying them blindly.
2. **Check applicable built-ins when the solution is open-ended.** Briefly inspect
   first-party rules and configuration-only mechanisms in
   [existing-analyzers.md](existing-analyzers.md). If the request explicitly targets a
   custom or approved rule, record relevant overlap and continue. If an existing rule
   is the chosen solution, configure it, validate it at step 7, and finish unless
   packaging or dogfooding applies.
3. **Define the diagnostic contract.** Choose a stable ID, category, default severity,
   help link, and release-tracking entry.
4. **Implement the analyzer and optional fix.** Follow [design.md](design.md), plus
   [symbol-actions.md](symbol-actions.md), [suppressors.md](suppressors.md), or
   [fix-all.md](fix-all.md) when applicable.
5. **Test behavior.** Follow [validation.md](validation.md), covering supported
   languages, false-positive boundaries, malformed code, fix eligibility, and Fix All.
6. **Measure relevant costs.** Use [performance.md](performance.md) for analyzer hot
   paths and representative bulk fixes. Scale expensive host probes to the change's
   risk and run them before broad enablement or performance claims.
7. **Validate adoption.** Test the configured or packaged analyzer against the actual
   consumer project and target graph. Confirm expected diagnostics, fixes, and build
   behavior before enabling it broadly.
8. **Package and dogfood deliberately.** Follow repository packaging conventions and
   verify the delivered assemblies. If dogfooding, prove the analyzer runs and scope
   intentional exemptions explicitly.

## Deep dives

- [existing-analyzers.md](existing-analyzers.md): applicable built-in and
  configuration alternatives.
- [design.md](design.md): analyzer and code-fix correctness.
- [fix-all.md](fix-all.md): provider selection, edit ordering, and bulk tests.
- [symbol-actions.md](symbol-actions.md): declaration-wide rule coverage.
- [release-tracking.md](release-tracking.md): shipped and unshipped diagnostics.
- [validation.md](validation.md): harnesses, edge cases, real-code and adoption checks.
- [performance.md](performance.md): IDE and bulk-fix costs.
- [suppressors.md](suppressors.md): suppressor ownership, implementation, and tests.
