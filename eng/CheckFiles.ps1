[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)]
  [String]$manifestPath,
  [Parameter(Mandatory=$true)]
  [String]$assetBasePath,
  [Parameter(Mandatory=$true)]
  [String]$outputFilePath
)
# Load the XML file
$xmlContent = [xml](Get-Content -Path $manifestPath)
# Get all files in the assets folder, including subfolders
$allFiles = Get-ChildItem -Path $assetBasePath -Recurse -File
# Ignore the last 3 digits of the version
function Adjust-Version {
    param (
        [string]$version
    )
    $versionPrefix = $version.Substring(0, $version.Length - 3)
    $versionSuffix = "\d+"
    return "$versionPrefix$versionSuffix"
}
# Normalize blob path when versions between manifest & files on disk may not match
function Get-ModifiedBlobPath {
    param (
        [string]$inputString
    )
    # Split the input string into parts using '/'
    $parts = $inputString.Split('/')
    # Function to modify the name in each part
    function Modify-NamePart($name) {
        # Use a regex pattern to match the last 3+ digit number surrounded by '-' or '.', or at the end of the string (with a '-' or '.' before it)
        $pattern = '(?<=[-.])(\d{3,})(?=[-.])|(?<=[-.])(\d{3,})$'
        # Find all matches
        $matches = [regex]::Matches($name, $pattern)
        # Fix build number
        if ($matches.Count -gt 0) {
            # Get the last match (the last 3+ digit number surrounded by '-' or '.')
            $lastMatch = $matches[$matches.Count - 1].Value
            # replace the last 3-digit number w/ a regex that matches any number of digits
            $newValue = "\d+"
            # Replace the last match with the new value
            $name = $name -replace [regex]::Escape($lastMatch), $newValue
        }
        # Fix date part of version suffix
        if ($matches.Count -gt 1) {
            # Get the last match (the last 3+ digit number surrounded by '-' or '.')
            $lastMatch = $matches[$matches.Count - 2].Value
            # replace the last 3-digit number w/ a regex that matches any number of digits
            $newValue = "\d+"
            # Replace the last match with the new value
            $name = $name -replace [regex]::Escape($lastMatch), $newValue
        }
        return $name
    }
    # Apply the operation to each part of the string
    $modifiedParts = $parts | ForEach-Object { Modify-NamePart $_ }
    # Join the parts back together with '/'
    $newString = $modifiedParts -join '/'
    if ($newString.StartsWith("assets/")) {
        $newString = $newString.Substring(7)  # Remove the first 7 characters ("assets/")
    }
    return $newString
}
$missingPackagesShipping = @()
$missingPackagesNonShipping = @()
$missingBlobsShipping = @()
$missingBlobsNonShipping = @()
$misclassifiedBlobsVmrShipping = @()
$misclassifiedBlobsVmrNonShipping = @()
# Iterate through each file in the assets folder
foreach ($file in $allFiles) {
    $foundMatchPackage = $false
    $foundMatchBlob = $false
    # Check if the file is under a folder named 'Packages'
    if ($file.FullName -match '\\Packages\\') {
        if ($file.Name -match "SourceBuild.Intermediate")
        {
            continue
        }
        # Get the package details from the XML file
        foreach ($package in $xmlContent.Build.Package) {
            $fileNameWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
            # Check if the file name matches the {Id}.{Version}.nupkg or {Id}.{Version}-nupkg pattern
            $id = $package.Id
            if ($fileNameWithoutExtension -match "^$id\.\d") {
                # Check for the RepoOrigin folder
                $repoOriginFolder = $file.FullName -match "\\$($package.RepoOrigin)\\"
                # Check if NonShipping is true, then look for 'nonshipping' folder in path
                $shippingFolder = if ($package.NonShipping -eq "true") {
                    $file.FullName -match '\\nonshipping\\'
                } else {
                    $file.FullName -match '\\shipping\\'
                }
                # If all conditions are met, print the match and the corresponding file
                if ($repoOriginFolder) {
                    $foundMatchPackage = $true
                    break
                }
            }
        }
        if (-not $foundMatchPackage)
        {
            if ( $file.FullName -match '\\nonshipping\\') {
                $missingPackagesNonShipping += $($file.FullName).Substring($assetBasePath.Length - 1)
            } else {
                $missingPackagesShipping += $($file.FullName).Substring($assetBasePath.Length - 1)
            }
            #Write-Host "No Match found for package '$($file.FullName)'"
        }
    }
    if ($file.FullName -notmatch '\\Packages\\') {
        if ($file.Name -eq "manifest.json" -or $file.Name -eq "release.json" -or $file.Name -eq "MergedManifest.xml" -or $file.Name -match "wixpack")
        {
            continue
        }
        foreach ($blob in $xmlContent.Build.Blob) {
            $fileNameWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
            # Check if the file name matches the {Id}.{Version}.nupkg or {Id}.{Version}-nupkg pattern
            $id = Get-ModifiedBlobPath($blob.Id)
            $repoOrigin = $blob.RepoOrigin
            if ($blob.DotNetReleaseShipping -eq "true")
            {
                $shippingExpected = "shipping"
                $shippingWrong = "nonshipping"
            }
            else
            {
                $shippingExpected = "nonshipping"
                $shippingWrong = "shipping"
            }
            $expectedLocation = "$assetBasePath/$repoOrigin/$shippingExpected/assets/$id" -replace '\\+', '/'
            $expectedLocation = $expectedLocation -replace '/d\+', '\d+'
            $unexpectedLocation = "$assetBasePath/$repoOrigin/$shippingWrong/assets/$id" -replace '\\+', '/'
            $unexpectedLocation = $unexpectedLocation -replace '/d\+', '\d+'
            $packageOnDisk = $file.FullName -replace '\\+', '/'
            if ($packageOnDisk -match $expectedLocation) {
                $foundMatchBlob = $true
                break
            } elseif ($packageOnDisk -match $unexpectedLocation) {
                if ($blob.DotNetReleaseShipping -eq "true")
                {
                    $misclassifiedBlobsVmrShipping += $($file.FullName).Substring($assetBasePath.Length - 1)
                }
                else
                {
                    $misclassifiedBlobsVmrNonShipping += $($file.FullName).Substring($assetBasePath.Length - 1)
                }
                # Write-Host "$packageOnDisk is in $shippingWrong but should be in $shippingExpected"
                $foundMatchBlob = $true
                break
            }
        }
        if (-not $foundMatchBlob)
        {
            if ( $file.FullName -match '\\nonshipping\\') {
                $missingBlobsNonShipping += $($file.FullName).Substring($assetBasePath.Length - 1)
            } else {
                $missingBlobsNonShipping += $($file.FullName).Substring($assetBasePath.Length - 1)
            }
            #Write-Host "No Match found for file '$($file.FullName)'"
        }
    }
}

New-Item -ItemType Directory -Force -Path $outputFilePath

$missingPackagesShipping | ForEach-Object { Add-Content -Path "$outputFilePath/MissingShippingPackages.txt" -Value $_ }
$missingPackagesNonShipping | ForEach-Object { Add-Content -Path "$outputFilePath/MissingNonShippingPackages.txt" -Value $_ }
$missingBlobsShipping | ForEach-Object { Add-Content -Path "$outputFilePath/MissingShippingBlobs.txt" -Value $_ }
$missingBlobsNonShipping | ForEach-Object { Add-Content -Path "$outputFilePath/MissingNonShippingBlobs.txt" -Value $_ }
$misclassifiedBlobsVmrShipping | ForEach-Object { Add-Content -Path "$outputFilePath/NonShippingBlobsMarkedShippingByVMR.txt" -Value $_ }
$misclassifiedBlobsVmrNonShipping | ForEach-Object { Add-Content -Path "$outputFilePath/ShippingBlobsMarkedNonShippingByVMR.txt" -Value $_ }