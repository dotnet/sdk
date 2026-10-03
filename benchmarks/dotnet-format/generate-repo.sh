#!/usr/bin/env bash
#
# Generates a synthetic C# solution used to benchmark `dotnet format`.
#
# The generated code is intentionally "unformatted" so the first formatting run
# has real work to do (mismatched end-of-line markers, missing final newlines,
# misaligned indentation). Subsequent runs have no work to do, which lets the
# benchmark show the difference between an empty and a warm content-hash cache.
#
# Usage:
#   generate-repo.sh <outputDir> <projects> <filesPerProject>
#
# Example:
#   generate-repo.sh /tmp/format-bench/repo 10 80
set -euo pipefail

OUT_DIR="${1:?output dir required}"
PROJECT_COUNT="${2:?project count required}"
FILES_PER_PROJECT="${3:?files per project required}"

mkdir -p "$OUT_DIR"

SLN_NAME="FormatBench"
PROJECTS=()

for ((p = 0; p < PROJECT_COUNT; p++)); do
  PROJ="Proj$(printf '%02d' "$p")"
  PROJ_DIR="$OUT_DIR/$PROJ"
  mkdir -p "$PROJ_DIR"

  cat > "$PROJ_DIR/$PROJ.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
EOF

  for ((f = 0; f < FILES_PER_PROJECT; f++)); do
    FILE_NAME="File$(printf '%03d' "$f").cs"
    {
      # Randomize a little so no two generated files are byte-identical.
      echo "using System;"
      echo "using System.Collections.Generic;"
      echo ""
      echo "namespace $PROJ"
      echo "{"
      echo "    // file $f of $PROJ"
      if ((f % 2 == 0)); then
        # CRLF endings, no final newline (written below with printf).
        printf '    public partial class Class%s\r\n    {\r\n        public int Value { get; set; } = %s;\r\n\r\n        public int Compute(int x) => x * %s;' "$f" "$((f * 7 + 1))" "$((f + 1))"
      else
        # Tabs and irregular spacing, missing final newline.
        printf '\tpublic partial class Class%s\n\t{\n\t\tpublic string Name { get; set; } = "class-%s";\n\n\t\tpublic string Describe()   =>\n\t\t\tName   + ":" +  Value();\n\n\t\tprivate string Value()\t=>  "v";\n' "$f" "$f"
      fi
    } > "$PROJ_DIR/$FILE_NAME"
  done

  PROJECTS+=("$PROJ_DIR/$PROJ.csproj")
done

# Write the solution file.
{
  echo ""
  echo "Microsoft Visual Studio Solution File, Format Version 12.00"
  echo "# Visual Studio Version 17"
  echo "VisualStudioVersion = 17.0.31903.59"
  echo "MinimumVisualStudioVersion = 10.0.40219.1"
  for p in "${!PROJECTS[@]}"; do
    PROJ="${PROJECTS[$p]}"
    GUID="$(printf 'FAED1DA2-0000-4A00-8ABC-A%07d' "$p")"
    echo "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Proj$(printf '%02d' "$p")\", \"$PROJ\", \"{$GUID}\""
    echo "EndProject"
  done
  echo "Global"
  echo "	GlobalSection(SolutionConfigurationPlatforms) = preSolution"
  echo "		Debug|Any CPU = Debug|Any CPU"
  echo "	EndGlobalSection"
  echo "	GlobalSection(ProjectConfigurationPlatforms) = postSolution"
  for p in "${!PROJECTS[@]}"; do
    GUID="$(printf 'FAED1DA2-0000-4A00-8ABC-A%07d' "$p")"
    echo "		{$GUID}.Debug|Any CPU.ActiveCfg = Debug|Any CPU"
    echo "		{$GUID}.Debug|Any CPU.Build.0 = Debug|Any CPU"
  done
  echo "	EndGlobalSection"
  echo "EndGlobal"
} > "$OUT_DIR/$SLN_NAME.sln"

# An .editorconfig that drives the whitespace formatters.
cat > "$OUT_DIR/.editorconfig" <<'EOF'
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
indent_size = 4
trim_trailing_whitespace = true

[*.cs]
csharp_new_line_before_open_brace = all
EOF

echo "Generated $PROJECT_COUNT projects x $FILES_PER_PROJECT files in $OUT_DIR"