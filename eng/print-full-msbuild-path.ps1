. $PSScriptRoot\common\tools.ps1
function InitializeCustomSDKToolset {
  if ($env:TestFullMSBuild -eq "true") {
     $env:DOTNET_SDK_TEST_MSBUILD_PATH = InitializeVisualStudioMSBuild -install:$false -vsRequirements:$GlobalJson.tools.'vs-opt'
  }
}

InitializeCustomSDKToolset
$env:DOTNET_SDK_TEST_MSBUILD_PATH
