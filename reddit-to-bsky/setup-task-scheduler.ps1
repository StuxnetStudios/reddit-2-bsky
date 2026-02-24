# Reddit-to-Bluesky Bot - Windows Task Scheduler Setup
# Run this script as Administrator in PowerShell

# Configuration
$taskName = "RedditToBsky"
$projectDir = "C:\workspace\web\reddit-to-bsky"
$exePath = "$projectDir\out\reddit-to-bsky.exe"
$taskArgs = "-a -n 1"
$intervalMinutes = 45

# Build/publish the project
Write-Host "Publishing project..."
Push-Location $projectDir
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o out
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "[OK] Published to $exePath"

# Verify executable exists
if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found after publish: $exePath"
    exit 1
}

# Set Bluesky credentials
Write-Host "Setting Bluesky credentials..."
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "stux-buddy@bsky.social", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "sx5k-e6zo-hggc-4sdi", "User")
Write-Host "[OK] Environment variables set"

# Remove existing task if it exists
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Removing existing task: $taskName"
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

# Create scheduled task action with -a -n 1 arguments
$action = New-ScheduledTaskAction -Execute $exePath -Argument $taskArgs -WorkingDirectory $projectDir\out

# Create trigger: repeat every 45 minutes indefinitely
$startTime = (Get-Date).AddMinutes(2)
$trigger = New-ScheduledTaskTrigger -Once -At $startTime `
    -RepetitionInterval (New-TimeSpan -Minutes $intervalMinutes) `
    -RepetitionDuration ([TimeSpan]::MaxValue)

# Settings: don't start a second instance if one is already running; run if missed
$settings = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 10)

# Run as current user (must be logged in)
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Highest

# Register the task
try {
    Register-ScheduledTask -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -Settings $settings `
        -Principal $principal `
        -Force `
        -ErrorAction Stop | Out-Null

    Write-Host ""
    Write-Host "[OK] Task scheduled successfully!"
    Write-Host ""
    Write-Host "Task Details:"
    Write-Host "  Name:      $taskName"
    Write-Host "  Exe:       $exePath"
    Write-Host "  Args:      $taskArgs"
    Write-Host "  Schedule:  Every $intervalMinutes minutes"
    Write-Host "  First run: $startTime"
    Write-Host "  User:      $env:USERNAME"
    Write-Host ""
    Write-Host "To run immediately:  Start-ScheduledTask -TaskName '$taskName'"
    Write-Host "To remove:           Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false"
}
catch {
    Write-Error "Failed to create scheduled task: $_"
    exit 1
}
