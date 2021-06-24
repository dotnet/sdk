#!/usr/bin/env bash
set -euo pipefail

script_root="$(cd -P "$( dirname "$0" )" && pwd)"

branch=master
branch_azdo=$branch

readme="$script_root/../README.md"

if [ ! -f "$readme" ]; then
  echo "$readme must exist."
  exit 1
fi

print_rows() {
  echo '| OS | *Azure DevOps*<br/>Release |'
  echo '| -- | :-- |'
  row 'CentOS7.1' 'Production'
  row 'CentOS7.1' 'Online'
  row 'CentOS7.1' 'Offline'
  row 'CentOS7.1' 'Offline Portable'
  row 'Debian8.2' 'Production'
  row 'Debian8.2' 'Online'
  row 'Fedora29' 'Production'
  row 'Fedora29' 'Online'
  row 'Fedora29' 'Offline'
  row 'Fedora29' 'Offline Portable'
  row 'OSX' 'Production'
  row 'Ubuntu16.04' 'Production'
  row 'Windows' 'Production'
}

raw_print() {
  printf '%s' "$1"
}

row() {
  os=$1
  job_type=$2
  display_name=$os
  if [ "$job_type" != "Production" ]; then
    display_name="$display_name ($job_type)"
  fi
  printf "| $display_name | "
  azdo
  end
}

end() {
  printf '\n'
}

azdo() {
  job=$(raw_print $os | awk '{print tolower($0)}' | sed 's/\.//g')

  # Fix case: AzDO has "sticky" casing across build def lifetime, so these names are inconsistent.
  # https://dev.azure.com/dnceng/internal/_workitems/edit/98
  case $os in
    OSX|Windows)
      job=$os
      ;;
  esac

  job_type_escaped=$(raw_print "$job_type" | sed 's/ /%20/g')
  query="?branchName=$branch_azdo&jobname=$job&configuration=$job_type_escaped"

  raw_print "[![Build Status](https://dev.azure.com/dnceng/internal/_apis/build/status/dotnet/source-build/source-build-CI$query)]"
  raw_print "(https://dev.azure.com/dnceng/internal/_build/latest?definitionId=114&branchName=$branch_azdo) | "
}

none() {
  raw_print '| '
}

cp "$readme" "$readme.old"

phase=before
while read line; do
  if [ "$phase" = before ]; then
    echo "$line"
    if [ "$line" = '<!-- Generated table start -->' ]; then
      print_rows
      phase=skip
    fi
  elif [ "$phase" = skip ]; then
    if [ "$line" = '<!-- Generated table end -->' ]; then
      echo "$line"
      phase=after
    fi
  else
    echo "$line"
  fi
done < "$readme.old" > "$readme"

rm "$readme.old"
