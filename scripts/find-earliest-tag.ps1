<#
.SYNOPSIS
Used to walk the history of tags in a repo and find the first tag containing a given commit.
#>
param (
    # the SHA whose parent we're looking for
    [Parameter(Mandatory = $true, Position=0)][string] $targetSHA
)

function Check-Ref {
    param (
        [string] $childRef,
        [string] $parentRef
    )
    Write-Host "Checking if $childRef is contained inside $parentRef" -ForegroundColor Yellow
    git merge-base --is-ancestor $childRef $parentRef
    return $?
}

function Get-Tags {
    param (
        [string] $remote
    )
    $tags=@{}

    # all the tags in '<SHA> <full tag name>' format (aka refs/tags/<thing we care about>)
    # sort by the tag creatordate, so we get the earliest tags first (because the build/tagging process creates new up-to-date tags with old numbers)
    foreach ($rawTag in git ls-remote --tags --sort=creatordate $remote v*) {
        $parts=($rawTag -split '\t')
        if ($parts.Length -eq 2) {
            $sha=$parts[0]
            $fullTag=$parts[1]
            $tag=($fullTag -split '/')[-1]
            if (-not ($tag.Contains("-") -or $tag -eq "v" -or $tag.Contains('^'))) {
                $tags.Add($tag, $sha)
            }
        }
    }
    return $tags.Keys.GetEnumerator()
}

$allTags=(Get-Tags "upstream")
$found=$false
foreach ($tag in $allTags) {
    if (Check-Ref $targetSHA $tag) {
        Write-Host "The earliest tag that is an ancestor of $targetSHA is $tag" -ForegroundColor Green
        $found=$true
        break;
    }
}
if (-not $found) {
    Write-Host "No tags found that are ancestors of $targetSHA" -ForegroundColor Red
}
