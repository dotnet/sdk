---
name: reproduce-sdk-issue
description: Reproduce a GitHub issue filed against the .NET SDK by reading the issue, downloading or creating a minimal reproduction project, and generating a binary log (binlog). Use this when you need to obtain a binlog for an SDK issue report that doesn't have one, or to verify that a reported problem is reproducible.
---

# Reproduce .NET SDK Issue

You are reproducing a GitHub issue to obtain a binary log (binlog) that captures the problematic .NET SDK behavior. Your goal is to ensure a `.binlog` file (or `.zip` containing one) exists that can be used for root cause analysis.

## Step 1: Read the issue

Use `gh issue view` to fetch the issue details:

- If you are given a full GitHub issue URL like `https://github.com/OWNER/REPO/issues/123`, extract `OWNER/REPO` and the issue number, then run:
  `gh issue view 123 --repo OWNER/REPO --json title,body,comments,labels`
- If you are only given an issue number for the .NET SDK repo, assume `dotnet/sdk` as the default:
  `gh issue view <number> --repo dotnet/sdk --json title,body,comments,labels`

Extract from the issue:
- **Symptom**: What the user observes (e.g., unexpected files in output, build error, wrong behavior).
- **Expected behavior**: What the user expected instead.
- **Reproduction steps**: How to reproduce the issue.
- **Attachments**: Look for linked binlog files (`.binlog` or `.zip` containing binlogs), reproduction projects (`.zip`, `.tar`), or inline project files.
- **Environment**: SDK version, OS, target framework, any special properties (`PublishAot`, `PublishSingleFile`, `SelfContained`, etc.).

## Step 2: Obtain or create a reproduction

### If the issue already includes a binlog

1. Download the binlog attachment using `gh` or `curl`.
2. If it is a `.zip` or `.tar`, extract it.
3. Verify the binlog loads correctly (e.g., check file size is non-zero).
4. You are done — report the path to the binlog.

### If the issue includes a reproduction project but no binlog

1. Download and extract the reproduction project.
2. Restore and build/publish/pack (matching the scenario described in the issue) with a binary log:
   ```bash
   dotnet restore
   dotnet publish -c Release /bl:1.binlog
   ```
   Name binlogs with incrementing numbers (`1.binlog`, `2.binlog`, etc.) if multiple builds are needed.
3. If the build/publish succeeds, inspect the output directory to confirm the reported symptom is present.
4. If the symptom is confirmed, the binlog captures the problematic behavior. You are done.

### If the issue has only a description (no attachments)

1. Create a minimal reproduction project in a temporary directory based on the issue description:
   - Use the simplest project type that reproduces the issue (e.g., `dotnet new console`, `dotnet new classlib`, `dotnet new webapi`).
   - Add only the properties and packages mentioned in the issue (e.g., `<PublishAot>true</PublishAot>`, `<PublishSingleFile>true</PublishSingleFile>`).
   - Keep the project as small as possible — the goal is a minimal reproduction, not a full application.
2. Build/publish/pack with a binary log:
   ```bash
   dotnet restore
   dotnet publish -c Release /bl:1.binlog
   ```
3. Inspect the output to confirm the reported symptom is present.
4. If the symptom is confirmed, the binlog captures the problematic behavior. You are done.

### If you cannot reproduce the issue

State clearly:
- What project configuration you tried.
- What commands you ran.
- What you observed (and how it differs from the reported symptom).
- Any possible reasons the reproduction failed (SDK version mismatch, OS-specific behavior, missing configuration).

Then stop — do not proceed to analysis without a confirmed reproduction.

## Step 3: Package the output

Once you have a binlog that captures the issue:

1. Note the absolute path to the binlog file.
2. If the reproduction project was created from scratch, keep it alongside the binlog so it can be used for future testing.
3. Summarize what you produced:
   - Path to binlog file(s).
   - The command that was run to produce each binlog.
   - Whether the reported symptom was confirmed.
   - Any relevant observations (e.g., "build failed at link stage as expected", "extra files appear in publish output as reported").

## Tips

- Match the user's environment as closely as possible: use the same SDK version, target framework, and build properties they reported.
- If the issue mentions a specific SDK version, check whether you have it installed. If not, note the version mismatch.
- Some issues only manifest during `publish`, not `build` — pay attention to which command the user ran.
- Some issues only manifest with specific configurations like `SelfContained`, `RuntimeIdentifier`, `PublishAot`, or `PublishSingleFile` — make sure these are set correctly in the reproduction project.
- If the issue involves multiple projects (e.g., a solution with several project references), reproduce the full structure rather than simplifying to a single project.
- When downloading attachments from GitHub issues, the URLs are typically in the issue body as markdown links. Use `curl -L` to follow redirects.
