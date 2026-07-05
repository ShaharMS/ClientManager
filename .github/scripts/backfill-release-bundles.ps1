param(
    [string[]]$Tag,
    [switch]$All
)

$ErrorActionPreference = 'Stop'
$RepoName = 'ClientManager'
$OutRoot = Join-Path $PSScriptRoot '.release-bundles'

function Get-PreviousAncestorTag {
    param([string]$CurrentTag)

    $previous = $null
    foreach ($candidate in (git tag -l --sort=v:refname)) {
        if ($candidate -eq $CurrentTag) { continue }
        git merge-base --is-ancestor "refs/tags/${candidate}^{commit}" "refs/tags/${CurrentTag}^{commit}" 2>$null
        if ($LASTEXITCODE -eq 0) { $previous = $candidate }
    }
    return $previous
}

function Build-ReleaseBundles {
    param([string]$CurrentTag)

    git rev-parse --verify "refs/tags/${CurrentTag}^{commit}" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Tag not found: $CurrentTag"
    }

    $outDir = Join-Path $OutRoot $CurrentTag
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $previous = Get-PreviousAncestorTag -CurrentTag $CurrentTag
    $files = @()

    $full = Join-Path $outDir "$RepoName-$CurrentTag.full.bundle"
    git bundle create $full "refs/tags/$CurrentTag"
    if ($LASTEXITCODE -ne 0) { throw "Failed to create full bundle for $CurrentTag" }
    git bundle verify $full | Out-Null
    $files += $full

    if ($previous) {
        $count = [int](git rev-list --count "$previous..$CurrentTag")
        if ($count -gt 0) {
            $incremental = Join-Path $outDir "$RepoName-$previous-to-$CurrentTag.bundle"
            git bundle create $incremental "refs/tags/$CurrentTag" --not "refs/tags/$previous"
            if ($LASTEXITCODE -ne 0) { throw "Failed to create incremental bundle for $CurrentTag" }
            git bundle verify $incremental | Out-Null
            $files += $incremental
        }
    }

    return [PSCustomObject]@{
        Tag = $CurrentTag
        Previous = $previous
        Files = $files
    }
}

if ($All) {
    $Tag = gh release list --limit 500 --json tagName -q '.[].tagName'
    if (-not $Tag) { throw 'No GitHub releases found.' }
}

if (-not $Tag) {
    throw 'Provide -Tag <name> or -All.'
}

git fetch --tags --force | Out-Null

foreach ($currentTag in $Tag) {
    Write-Host "Building bundles for $currentTag..."
    $result = Build-ReleaseBundles -CurrentTag $currentTag
    if ($result.Previous) {
        Write-Host "  Previous ancestor tag: $($result.Previous)"
    }
    foreach ($file in $result.Files) {
        Write-Host "  Uploading $(Split-Path $file -Leaf)..."
        gh release upload $currentTag $file --clobber
        if ($LASTEXITCODE -ne 0) { throw "Failed to upload $file to release $currentTag" }
    }
}

Write-Host 'Done.'
