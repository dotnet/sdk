# Writing the impact summary

Detail for the [aot-impact-analysis](SKILL.md) skill. How to turn the two report
files into the PR-ready summary a reviewer can act on.

## Combine the reports

```powershell
Get-Content artifacts/aot-size-win-x64.md, artifacts/aot-startup.md |
    Set-Content artifacts/aot-impact.md
```

Size table first, then startup. If only size was run (the common case — startup
needs a full SDK layout), the size report alone is the summary.

## What a good summary says, in order

1. **Headline native-binary delta** — the raw `dotnet-aot.dll` change in KB and
   %. This is the number reviewers anchor on.
2. **Accounted total** — the `sizoscope-cli` `Total accounted size difference`,
   which attributes that delta to assemblies/types.
3. **Top contributors and *why*** — the largest grown assemblies, each mapped to
   a feature of the PR (e.g. a crypto assembly ← in-process dev-cert generation;
   registry/ACL assemblies ← workload detection). A contributor that maps to
   nothing is a flag, not a footnote — call it out as a possible unintended
   dependency.
4. **Startup delta** — median and P95 managed-vs-AOT (or build-vs-build), only if
   the startup phase ran. State the scenario/arguments measured.
5. **Caveats** — anything not validated locally (e.g. a platform whose path
   couldn't be exercised on this machine), and the baseline ref used.

## Discipline

- Keep every claim tied to a number a script produced; don't editorialize past
  the data.
- Ignore the equal `+`/`-` `*.resources` rows when narrating growth — they are
  satellite re-attribution, not real cost (see
  [size-analysis.md](size-analysis.md)).
- Report size and startup as **independent** signals: neither implies the other.
- Note the RIDs measured. CI covers **win-x64** and **osx-arm64**; if you only
  ran one locally, say so.
