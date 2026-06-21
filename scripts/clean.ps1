$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$targets = @(
    "BranchPOS\bin",
    "BranchPOS\obj",
    "BranchPOS.Tests\bin",
    "BranchPOS.Tests\obj",
    ".vs",
    "TestResults"
)

$allowedPrefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

foreach ($relativePath in $targets) {
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $relativePath))

    if (-not $fullPath.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a path outside the repository: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
        Write-Host "Removed $relativePath"
    }
    else {
        Write-Host "Skipped $relativePath (not present)"
    }
}

Write-Host "Repository cleanup complete. Source files and migrations were not changed."
