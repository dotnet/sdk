#!/bin/bash

# Define the Validate flag. Use 0 for false as default.
Validate=0

# Define help function
function help {
  echo "Usage: $0 [--validate] [--help]"
  echo
  echo "Options:"
  echo "  --validate    Run the script in validate mode"
  echo "  --help        Display this help message"
  exit 1
}

# Parse command-line arguments
for arg in "$@"; do
  case $arg in
    --validate)
      Validate=1
      shift
      ;;
    --help)
      help
      ;;
    *)
      echo "Unknown option: $arg"
      help
      ;;
  esac
done

# Define the RepoRoot and TestProjects
RepoRoot="$(dirname "$(dirname "$(dirname "$(realpath "$0")")")")"
echo "RepoRoot is: $RepoRoot"

TestProjects=("Microsoft.NET.Sdk.Razor.Tests" "Microsoft.NET.Sdk.BlazorWebAssembly.Tests")

# Add the test path to the TestProjects
for i in "${!TestProjects[@]}"; do
  TestProjects[i]="$RepoRoot/test/${TestProjects[i]}"
done

echo "TestProjects are:"
for TestProject in "${TestProjects[@]}"; do
  echo "$TestProject"
done

# Run the dotnet test command based on the Validate flag
if [ $Validate -eq 1 ]; then
  echo "Running in validate mode"
  for TestProject in "${TestProjects[@]}"; do
    echo "Running dotnet test on $TestProject"
    dotnet test --no-build -c Release -l "console;verbosity=normal" "$TestProject" --filter AspNetCore=BaselineTest
  done
else
  echo "Running in non-validate mode"
  for TestProject in "${TestProjects[@]}"; do
    echo "Running dotnet test on $TestProject"
    dotnet test --no-build -c Release -l "console;verbosity=normal" "$TestProject" -e ASPNETCORE_TEST_BASELINES=true --filter AspNetCore=BaselineTest
  done
fi
