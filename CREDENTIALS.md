# Bluesky Credentials Setup - Environment Variables

This guide explains how to securely store your Bluesky credentials using Windows environment variables.

## Why Environment Variables?

- Simple and secure for local use
- No credentials stored in code or config files
- Survives app restarts and updates
- Works with Windows Task Scheduler
- User-level isolation (only your account can access)

## Step 1: Generate Bluesky App Password

1. Go to https://bsky.app and login
2. Click your profile icon (top-right)
3. Settings > App Passwords
4. Click "Create App Password"
5. Give it a name like "Reddit2Bluesky"
6. Copy the password (you'll only see it once!)

Save the password in a safe place temporarily.

## Step 2: Set Environment Variables (One-Time Setup)

Open **PowerShell as Administrator** and run:

```powershell
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "stux-@bsky.social", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "testiestesties", "User")
```

Replace:
- `your-handle@bsky.social` - Your actual Bluesky handle
- `your-app-password-here` - The app password you generated above

Example:
```powershell
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "stux-buddy@bsky.social", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "testiestesties", "User")
```

## Step 3: Verify Setup

In PowerShell, verify the variables are set:

```powershell
$env:BLUESKY_HANDLE
$env:BLUESKY_APP_PASSWORD
```

Should display your handle and app password.

## Step 4: Restart Applications

For Task Scheduler or other apps to pick up the variables, you may need to:

1. **Close all PowerShell windows**
2. **Restart the application** or machine (for Task Scheduler)

## Security Notes

- Environment variables are stored securely in Windows Registry (`HKEY_CURRENT_USER\Environment`)
- Only accessible to your user account
- Not visible in logs or process listings
- Cannot be read by other users on the same machine

## If You Need to Change Credentials

```powershell
# Update with new values
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "stux-buddy@bsky.social", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "testiestesties", "User")
```

## If You Need to Remove Credentials

```powershell
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", $null, "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", $null, "User")
```

## Troubleshooting

### "Bluesky credentials not set" error

Make sure:
1. Variables are set to **User** scope (not System)
2. PowerShell was run as Administrator
3. You restarted the application after setting variables
4. Variable names are exactly: `BLUESKY_HANDLE` and `BLUESKY_APP_PASSWORD`

Check values:


Get-Item Env:BLUESKY_HANDLE
Get-Item Env:BLUESKY_APP_PASSWORD
```

### Task Scheduler not picking up credentials

- Restart the machine after setting variables
- Or manually update the task trigger in Task Scheduler

### "Wrong credentials" from Bluesky API

- Double-check the app password (copy/paste to avoid typos)
- Make sure it's an app password, not your main account password
bool success = await BlueskyClient.PostAsync(post.Title, imagePath);

## Example: Full Setup

```powershell
# 1. Open PowerShell as Administrator
# 2. Run these commands (replace with your actual values):

[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "stux-buddy", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "testiestesties", "User")

# 3. Verify:
$env:BLUESKY_HANDLE
if ($env:BLUESKY_APP_PASSWORD) { Write-Host "BLUESKY_APP_PASSWORD is set (value hidden for security)." }

# 4. Close PowerShell
# 5. Open a new PowerShell window to confirm variables persist
# 6. Run the bot:
C:\workspace\web\reddit-to-bsky\out\reddit-to-bsky.exe -a

# Should see: "[OK] Logged in to Bluesky as john@bsky.social"