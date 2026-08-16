# Clean build script for MonoRogue
# Removes all bin/obj folders below the repository root and runs a fresh build.

param(
    [string]$SolutionPath = "C:\Users\Dustin\MonogameProjects\MonoRogue\MonoRogue.slnx",
    [switch]$NoBuild
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Repository root (script location): $repoRoot"

# Remove bin and obj directories
Write-Host "Removing bin and obj directories..."
Get-ChildItem -Path $repoRoot -Directory -Recurse -Force | Where-Object { $_.Name -in @('bin','obj') } | ForEach-Object {
    Write-Host "Removing: $($_.FullName)"
    Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $NoBuild) {
    Write-Host "Running dotnet build: $SolutionPath"
    dotnet build $SolutionPath --no-incremental
} else {
    Write-Host "Skipping build (--NoBuild specified)."
}

