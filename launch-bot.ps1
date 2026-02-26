# PowerShell launcher for Reddit-to-Bluesky bot
# Usage: .\launch-bot.ps1 [-Subreddits <string[]>] [-All] [-Limit <int>]
# Examples:
#   .\launch-bot.ps1 -All                # run all subreddits
#   .\launch-bot.ps1 r/Conservative     # run one subreddit
#   .\launch-bot.ps1 -All -Limit 1      # run once limit

param(
    [string[]]$Subreddits,
    [switch]$All,
    [int]$Limit = 1
)

# You can also set or verify env vars here, e.g.:
# $env:BLUESKY_HANDLE = "stux-buddy@bsky.social"
# $env:BLUESKY_APP_PASSWORD = "your-app-password"

$exe = Join-Path $PSScriptRoot "out\reddit-to-bsky.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Executable not found: $exe. Please publish the project first."
    exit 1
}

$args = @()
if ($All) { $args += '-a' }
if ($Subreddits) { $args += $Subreddits }
if ($Limit -gt 0) { $args += '-n'; $args += $Limit }

Write-Host "Running bot with arguments:`n  $($args -join ' ')" -ForegroundColor Cyan

& $exe $args
$exitCode = $LASTEXITCODE
Write-Host "Bot exited with code $exitCode" -ForegroundColor Yellow
exit $exitCode
