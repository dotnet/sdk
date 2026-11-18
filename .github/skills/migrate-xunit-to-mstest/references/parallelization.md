# Parallelization: xUnit -> MSTest

Detailed guidance for Step 11 of the [`migrate-xunit-to-mstest`](../SKILL.md) skill. Step numbers refer to that skill's workflow.

## How each framework parallelizes by default

| Framework | Across test classes | Within a test class | Test-class instance lifetime |
|---|---|---|---|
| **xUnit v2** | Parallel (one class per worker thread) | Serial (one test method at a time) | New instance per test method |
| **xUnit v3** | Parallel (same as v2) | Serial (same as v2) | New instance per test method |
| **MSTest (default)** | Serial (one class at a time) | Serial (one test method at a time) | New instance per test method |
| MSTest + `[assembly: Parallelize(Scope = ClassLevel)]` | Parallel | Serial | Same |
| MSTest + `[assembly: Parallelize(Scope = MethodLevel)]` | Parallel | **Parallel** -- more aggressive than xUnit | Same |

`Workers = 0` means "use all available logical cores" (MSTest's recommended default for parallel runs); any positive integer caps the worker count.

## Pick a target model -- there are three reasonable choices

**Choice A -- Match xUnit's behaviour exactly (recommended default):**

```csharp
// Place in any .cs file at assembly scope (often AssemblyInfo.cs or GlobalUsings.cs)
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.ClassLevel)]
```

Use this when the suite was healthy on xUnit and you want zero behavioural change. It preserves "parallel across classes, serial within a class" exactly.

> **REQUIRED -- explicitly tell the user why this attribute is needed.** When applying Choice A, include this sentence (verbatim or near-verbatim) in your final summary:
>
> "MSTest defaults to **serial** execution across classes (unlike xUnit, which parallelizes classes by default), so this `[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.ClassLevel)]` is **required** to match the project's previous xUnit parallel-class behaviour. Without it, the suite would still pass but run roughly one-class-at-a-time and CI throughput would drop."
>
> The user must understand this is **opt-in** under MSTest -- a silent omission looks like a no-op but is actually a behavioural regression.

**Choice B -- Adopt MSTest's serial default:**

```csharp
// No [assembly: Parallelize] needed -- this is the default
```

Use this only when the suite has known shared-state issues (Step 1.6) that you intend to leave unfixed for now, or when wall-clock time is not a concern. Expect significantly slower CI.

**Choice C -- Selective parallelization:**

```csharp
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.ClassLevel)]
```

Plus per-class opt-out for the classes that genuinely cannot run concurrently:

```csharp
[TestClass]
[DoNotParallelize]
public sealed class DatabaseIntegrationTests { /* ... */ }
```

Use this when most of the suite is isolated but a few classes touch shared state (one DB, fixed ports, file system locations). This is usually the right answer when migrating from xUnit collections.

> **Do not pick `ExecutionScope.MethodLevel` to "match xUnit"** -- it parallelizes test methods *within* a single class, which xUnit never does. It is more aggressive than xUnit and will surface latent intra-class state issues.

## Translate xUnit parallelization opt-outs

| xUnit pattern | MSTest equivalent |
|---|---|
| `[assembly: CollectionBehavior(DisableTestParallelization = true)]` | Omit `[assembly: Parallelize]` (or use Choice B above) |
| `[assembly: CollectionBehavior(MaxParallelThreads = N)]` | `[assembly: Parallelize(Workers = N, Scope = ExecutionScope.ClassLevel)]` |
| `[Collection("Db")]` on multiple classes (forces those classes to share a fixture **and** run serially) | `[DoNotParallelize]` on each of those classes (preserves serialization) + Step 8 fixture handling (preserves sharing) |
| `[CollectionDefinition("Db", DisableParallelization = true)]` | Same as above -- `[DoNotParallelize]` on each member class |
| `[Collection("Foo")]` used only for fixture sharing (no parallelization concern) | Step 8 fixture handling; **do not** add `[DoNotParallelize]` |

The distinction in the last two rows matters: xUnit collections conflate "share state" with "serialize". MSTest decouples them. Read the original `[CollectionDefinition]` carefully -- if `DisableParallelization` is `false` (or omitted), only the fixture sharing semantic needs to migrate, not the serialization.

## Verify after Step 13

If pass/fail counts diverge from the baseline after migration, parallelization is the first place to look:

- **More failures than baseline**: tests are now running concurrently and stomping shared state. Either add `[DoNotParallelize]` to the offending classes, or fix the shared state.
- **Fewer failures than baseline** (tests previously flaky now green): probably means a race condition that xUnit's scheduling exposed is now hidden by serial execution. Note it in a follow-up issue -- do not declare victory.
- **Same count but tests take much longer**: you forgot `[assembly: Parallelize]`. Add Choice A.
- **Same count but tests take much less time and occasionally fail**: you picked `MethodLevel` instead of `ClassLevel`. Switch to `ClassLevel`.

## Other runner config: `xunit.runner.json` migration

Delete `xunit.runner.json`. Port relevant settings:

| `xunit.runner.json` | MSTest equivalent |
|---|---|
| `"parallelizeAssembly": false` | Default in MSTest -- no action |
| `"parallelizeTestCollections": false` | Omit `[assembly: Parallelize]` (Choice B) |
| `"maxParallelThreads": N` | `[assembly: Parallelize(Workers = N, Scope = ExecutionScope.ClassLevel)]` |
| `"methodDisplay": "method"` / `"classAndMethod"` | No equivalent (MSTest always uses class + method) |
| `"diagnosticMessages": true` | Use `--diagnostic` on the CLI, or set verbosity in `.runsettings` |
| `"preEnumerateTheories": false` | No equivalent (MSTest enumerates `[DataRow]`/`[DynamicData]` eagerly) |
| `"longRunningTestSeconds": N` | Use `[Timeout(N * 1000)]` per test |
| `"appDomain": "denied"` / `"ifAvailable"` | No equivalent (MSTest uses no app domains on modern .NET) |

If the project uses xUnit traits in CI filter expressions (e.g., `--filter "Category=Unit"` with xUnit), the equivalent MSTest filter is `--filter "TestCategory=Unit"` (VSTest) or `--filter-trait "TestCategory=Unit"` (MTP). Update CI pipelines accordingly.
