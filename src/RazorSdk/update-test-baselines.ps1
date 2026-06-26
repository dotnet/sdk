param([switch] $Validate)
$RepoRoot= Resolve-Path "$PSScriptRoot/../.."

$TestProjects = "Microsoft.NET.Sdk.StaticWebAssets.Tests", "Microsoft.NET.Sdk.Razor.Tests", "Microsoft.NET.Sdk.BlazorWebAssembly.Tests" |
 ForEach-Object { Join-Path -Path "$RepoRoot/test/" -ChildPath $_ };

if($Validate){
  $TestProjects | ForEach-Object { dotnet test --project $_ --no-build -c Release -v normal --filter "TestCategory=BaselineTest" }
}else {
  $TestProjects | ForEach-Object { dotnet test --project $_ --no-build -c Release -v normal --environment ASPNETCORE_TEST_BASELINES=true --filter "TestCategory=BaselineTest" }
}
