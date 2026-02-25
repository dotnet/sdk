
---
applyTo: "**"
---
- Environment: Windows 11 using PowerShell 7
- Never use `&&` to chain commands; use semicolon (`;`) for PowerShell command chaining
- Prefer PowerShell cmdlets over external utilities when available
- Use PowerShell-style parameter syntax (-Parameter) rather than Unix-style flags

Code Style:
- An `.editorconfig` at `src/Installer/.editorconfig` governs all dotnetup code. Follow it strictly for new code.
- Key conventions: file-scoped namespaces, `s_` prefix for static fields, `_` prefix for instance fields, file headers, sorted usings, `ConfigureAwait(false)`, `CultureInfo.InvariantCulture` for formatting.
- All projects use `TreatWarningsAsErrors`. Style violations break the build.
- To auto-format a project: `d:\sdk\.dotnet\dotnet format <project.csproj> --no-restore`
- When debugging or iterating on a bug fix, it is fine to temporarily ignore style issues and fix them afterward. Prefer working code over perfect formatting during active troubleshooting.
- Do not reformat unrelated code in the same commit as a bug fix — keep formatting changes in separate commits to preserve clean git blame.
