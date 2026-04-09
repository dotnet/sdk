---
description: "Generates unit tests for code introduced in a pull request when a contributor comments /add-tests."

on:
  slash_command:
    name: add-tests
    events: [pull_request_comment]
  roles: [admin, maintainer, write]

permissions:
  contents: read
  pull-requests: read

imports:
  - shared/repo-build-setup.md

tools:
  github:
    toolsets: [pull_requests, repos]
  edit:
  bash: ["dotnet", "git", "find", "ls", "cat", "grep", "head", "tail", "wc", "mkdir"]

safe-outputs:
  create-pull-request:
    title-prefix: "[tests] "
    labels: [test, automated]
    draft: true
    max: 1
    protected-files: fallback-to-issue
  add-comment:
    max: 3

timeout-minutes: 60
---

# Add Tests for PR Changes

Generate comprehensive unit tests for the code changes introduced in pull request #${{ github.event.issue.number }}.

## Context

The PR comment that triggered this workflow: "${{ steps.sanitized.outputs.text }}"

## Goal

Analyze the pull request diff to identify source files that were added or modified, then generate unit tests that cover those changes. The resulting tests should be submitted as a new draft pull request.

## Instructions

### Step 1: Understand the PR Changes

1. Use the GitHub pull requests tools to fetch the PR diff for PR #${{ github.event.issue.number }}
2. Identify all **source files** (under `src/`) that were added or modified — ignore test files, build files, docs, and config
3. For each changed source file, understand what classes, methods, or functionality was added or changed

### Step 2: Identify Test Gaps

1. For each changed source file, find the corresponding existing test project under `test/`
2. Check if the changed code already has test coverage
3. Focus on code that is **not yet covered** by existing tests

### Step 3: Generate Tests

Use the `task` tool to invoke the `code-testing-generator` agent (defined at `.github/agents/code-testing-generator.agent.md`) as a **subagent**:

```
task({
  agent: "code-testing-generator",
  prompt: "Generate unit tests for the changed source files in this PR. Follow the Research → Plan → Implement pipeline. Use the `task` tool to invoke all sub-agents (code-testing-researcher, code-testing-planner, code-testing-implementer, etc.) as subagents."
})
```

**Important**: The `code-testing-generator` agent MUST use the `task` tool to invoke its own sub-agents (researcher, planner, implementer) as subagents. Do NOT inline the sub-agent work — each sub-agent must be called via the `task` tool.

Follow these SDK-specific conventions:

1. Follow the Research → Plan → Implement pipeline from the skill
2. **Scope**: Only generate tests for code modified in this PR — do not attempt full-repo coverage
3. **Test framework**: This repo uses xUnit with `[Fact]`, `[Theory]`, `[InlineData]` attributes. Some projects also use MSTest
4. **Naming**: Test classes as `{Feature}Tests`, test methods as PascalCase descriptive names
5. **License header**: Every `.cs` file must start with the 3-line .NET Foundation MIT license header:
   ```
   // Licensed to the .NET Foundation under one or more agreements.
   // The .NET Foundation licenses this file to you under the MIT license.
   // See the LICENSE file in the project root for more information.
   ```
6. **Style**: Follow existing test patterns in the repo — check adjacent test files for conventions
7. **Test projects**: When creating new test projects in `test/`, always use `$(CurrentTargetFramework)` for the `<TargetFramework>` property instead of hard-coding a specific version
8. **Build**: Use `dotnet build <TestProject.csproj>` for scoped builds during development
9. **Test execution**:
   - For MSTest-style projects: `dotnet test path/to/project.csproj --filter "FullyQualifiedName~TestName"`
   - For XUnit test assemblies: `dotnet exec artifacts/bin/redist/Debug/TestAssembly.dll -method "*TestMethodName*"`
10. **Skip parameter**: When using the `Skip` parameter of the `[Fact]` attribute, point to a specific issue link
11. **Do not modify `.xlf` files** — if localization files need updating, note it in the PR description

### Step 4: Validate

1. Build the specific test project(s) you modified
2. Run the tests to verify they pass
3. If tests fail, fix assertions based on actual production code behavior — never skip or ignore tests

### Step 5: Create the PR

Commit all test files and create a draft pull request. The PR description should:
- Reference the original PR (#${{ github.event.issue.number }})
- List the test files created
- Summarize what is covered by the new tests
