#current_file_dir=`dirname $0`
#repo_root=${current_file_dir}/..
#echo "$@"
#
## get all text in list file
## join lines by |
## replace all "|" to "/\" to from pattern a.csproj or pattern b.csproj
#all_projects_cannot_run_in_helix_with_partial_file_names_pattern=`cat ${repo_root}/src/Tests/testsProjectCannotRunOnHelixList.txt | paste -sd "|" - | sed 's/|/\\\|/g' `
#
## get all tests projects paths filter by the pattern. To get full path of all tests cannot be run in Helix
#all_projects_path_cannot_run_in_helix=`find ${repo_root}/src/Tests  -iregex '^.*.Tests.csproj' | grep ${all_projects_cannot_run_in_helix_with_partial_file_names_pattern}`
#
#realpath() {
#    [[ $1 = /* ]] && echo "$1" || echo "$PWD/${1#./}"
#}
#
#for project_path in $all_projects_path_cannot_run_in_helix
#do
#    ${repo_root}/eng/common/build.sh --test "$@" --projects `realpath $project_path`
#done

$repoRoot = (get-item $PSScriptRoot).parent
#echo (get-item $PSScriptRoot).parent.GetDirectories("src").GetDirectories("Tests")
$allTests = Get-Childitem -Path $repoRoot.GetDirectories("src").GetDirectories("Tests").FullName -Recurse '*.Tests.csproj'

$testsProjectCannotRunOnHelixListPath = $repoRoot.GetDirectories("src").GetDirectories("Tests").GetFiles("testsProjectCannotRunOnHelixList.txt").FullName
$buildshScriptPath = $repoRoot.GetDirectories("eng").GetDirectories("common").GetFiles("build.ps1").FullName

$args = $args
$passin = @("-test")
$passin = $passin  +  $args
#echo $passin  
foreach($line in ([System.IO.File]::ReadLines($testsProjectCannotRunOnHelixListPath) | Where-Object { $_.Trim() -ne '' }))
{
    foreach ($testProject in $allTests)
    {
        if ($testProject.FullName.Contains($line.Trim())) { 
            $passInArgs = @("-test") + $args + @("-projects", $testProject.FullName)
            #echo $passInArgs
            #& $buildshScriptPath $passInArgs
            #Invoke-Command -NoNewScope -ArgumentList $ArgumentList -ScriptBlock $ScriptBlock -Verbose
            #& "C:\AzureFileShare\MEDsys\Powershell Scripts\B.ps1" -ServerName medsys-dev

            Invoke-Expression "& $buildshScriptPath $passInArgs"
        }
    }
}
