# Reddit-to-Bluesky Bot - Deployment Guide

## Prerequisites

- Windows 11 with .NET 8.0 or later
- Bluesky account with app password
- Administrator access (for Task Scheduler)

## Quick Start

### Step 1: Build the Executable

```bash
cd c:\workspace\web\reddit-to-bsky
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o out
```

This creates: `out\reddit-to-bsky.exe` (34.7 MB, includes .NET runtime)

### Step 2: Generate Bluesky App Password

1. Go to https://bsky.app
2. Login to your account
3. Settings > App Passwords > Create App Password
4. Save the password (you'll only see it once)

### Step 3: Set Environment Variables

Open PowerShell as Administrator:

```powershell
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "your-handle@bsky.social", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "your-app-password", "User")
```

### Step 4: Schedule the Task

Run as Administrator:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
C:\workspace\web\reddit-to-bsky\setup-task-scheduler.ps1
```

### Step 5: Verify Setup

Check Task Scheduler:

```powershell
Get-ScheduledTask -TaskName "RedditToBsky"
Start-ScheduledTask -TaskName "RedditToBsky"  # Manual test run
```

View logs:

```powershell
tail -f C:\workspace\web\reddit-to-bsky\posted.db  # Database
Get-Content C:\workspace\web\reddit-to-bsky\bot.log  # Log file
```

## Manual Execution

To run manually:

```bash
cd C:\workspace\web\reddit-to-bsky
dotnet run
```

Or use the compiled executable:

```bash
C:\workspace\web\reddit-to-bsky\out\reddit-to-bsky.exe
```

## Troubleshooting

### "Bluesky credentials not set"
- Verify environment variables are set:
```powershell
$env:BLUESKY_HANDLE
$env:BLUESKY_APP_PASSWORD
```

### Task runs but doesn't post
- Check bot.log for errors
- Verify Bluesky credentials are correct
- Check rate limits (Bluesky may throttle requests)

### Database locked
- Ensure only one instance is running
- Delete `posted.db` to reset tracking (will repost old content)

## File Structure

```
reddit-to-bsky/
├── Program.cs              # Main entry point
├── *.cs                    # Source files
├── out/                    # Published executable
│   └── reddit-to-bsky.exe  # Single standalone EXE
├── bin/                    # Build artifacts
├── posted.db               # SQLite database (created at runtime)
├── bot.log                 # Application log
└── README.md               # Documentation
```

## Customization

Edit `appsettings.json` to change:
- Subreddit (default: Conservative)
- Minimum score threshold (default: 200)
- Fetch limit per cycle (default: 100)

## Uninstalling

To remove the scheduled task:

```powershell
Unregister-ScheduledTask -TaskName "RedditToBsky" -Confirm:$false
```

To clean up files:

```bash
rmdir /s /q C:\workspace\web\reddit-to-bsky
```
