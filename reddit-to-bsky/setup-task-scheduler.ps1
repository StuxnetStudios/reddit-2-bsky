# Reddit-to-Bluesky Bot - Windows Task Scheduler Setup
# Run this script as Administrator in PowerShell

# Configuration
$taskName = "RedditToBsky"
$exePath = "C:\workspace\web\reddit-to-bsky\out\reddit-to-bsky.exe"
$logDir = "C:\workspace\web\reddit-to-bsky\logs"

# Verify executable exists
if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found: $exePath"
    Write-Host "Please build the project first: dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o out"
    exit 1
}

# Create logs directory
if (-not (Test-Path $logDir)) {
    New-Item -Path $logDir -ItemType Directory | Out-Null
    Write-Host "[OK] Created logs directory: $logDir"
}

# Set Bluesky credentials (update these with your values)
Write-Host ""
Write-Host "Setting Bluesky credentials..."
Write-Host "If you haven't already, update these environment variables:"
Write-Host ""
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "your-handle@bsky.social", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "your-app-password", "User")
Write-Host "[OK] Environment variables set"
Write-Host ""

# Remove existing task if it exists
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Removing existing task: $taskName"
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

# Create scheduled task action
$action = New-ScheduledTaskAction -Execute $exePath

# Create trigger (hourly, starting 5 minutes from now)
$startTime = (Get-Date).AddMinutes(5)
$trigger = New-ScheduledTaskTrigger -Hourly -At $startTime

# Create task settings
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable

# Create principal (run as current user with highest privileges)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType ServiceAccount -RunLevel Highest

# Register the task
try {
    Register-ScheduledTask -TaskName $taskName `
        -Action $action `
        -Trigger $trigger `
        -Settings $settings `
        -Principal $principal `
        -ErrorAction Stop | Out-Null
    
    Write-Host "[OK] Task scheduled successfully!"
    Write-Host ""
    Write-Host "Task Details:"
    Write-Host "  Name: $taskName"
    Write-Host "  Executable: $exePath"
    Write-Host "  Schedule: Hourly"
    Write-Host "  First run: $startTime"
    Write-Host "  User: $env:USERNAME"
    Write-Host ""
    Write-Host "The bot will now run automatically every hour."
    Write-Host "Logs will be written to: $logDir"
    Write-Host ""
    Write-Host "To check logs:"
    Write-Host "  dir $logDir"
    Write-Host "  type $logDir\bot.log"
}
catch {
    Write-Error "Failed to create scheduled task: $_"
    exit 1
}
