---
coverage: ProjectData host-writer precedence and standalone SDK fallback
---

# Architecture

## ProjectData Import Precedence

The final ProjectData import in
[`Sdk.targets`](../../src/Tasks/Microsoft.NET.Build.Tasks/sdk/Sdk.targets) preserves a
writer already imported through `CustomAfterMicrosoftCommonTargets` or
`CustomAfterMicrosoftCommonCrossTargetingTargets`. The shared writer sets
`_ProjectDataTaskAssembly` during evaluation; a non-empty value prevents a second
SDK-bundled writer from replacing the host's target definitions and task-assembly
identity. Without a host writer, the SDK still imports its bundled targets when present.
Both evaluation shapes and the standalone SDK behavior are covered by
[`GivenThatWeWantToUseProjectData`](../../test/Microsoft.NET.Build.Tests/GivenThatWeWantToUseProjectData.cs).
