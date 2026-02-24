# Reddit-to-Bluesky Bot — Shutdown Checklist

Use this checklist any time you need to stop, pause, or decommission the bot.
Tick each step in order. Steps marked **[OPTIONAL]** can be skipped for a quick pause.

---

## 1 · Stop Scheduled Execution

- [ ] **Disable the Task Scheduler task** (keeps the task registered but stops future runs)
  ```powershell
  Disable-ScheduledTask -TaskName "RedditToBsky"
  ```
- [ ] Confirm no pending run is active:
  ```powershell
  Get-ScheduledTask -TaskName "RedditToBsky" | Select-Object State, LastRunTime, NextRunTime
  ```

---

## 2 · Kill Any Running Processes

- [ ] Check whether the exe is currently running:
  ```powershell
  Get-Process -Name "reddit-to-bsky" -ErrorAction SilentlyContinue
  ```
- [ ] If running, stop it gracefully:
  ```powershell
  Stop-Process -Name "reddit-to-bsky" -Force
  ```

---

## 3 · Verify Bot Has Stopped

- [ ] Confirm process list is empty:
  ```powershell
  Get-Process -Name "reddit-to-bsky" -ErrorAction SilentlyContinue
  # Should return nothing
  ```
- [ ] Tail the log to confirm no activity since shutdown:
  ```powershell
  Get-Content "C:\workspace\web\reddit-to-bsky\logs\bot.log" -Tail 20
  ```

---

## 4 · Back Up State (SQLite Database)

- [ ] Copy the database to a timestamped backup:
  ```powershell
  $ts = Get-Date -Format "yyyyMMdd_HHmmss"
  Copy-Item "C:\workspace\web\reddit-to-bsky\out\posted.db" `
            "C:\workspace\web\reddit-to-bsky\out\posted.$ts.db.bak"
  ```
- [ ] [OPTIONAL] Verify backup integrity:
  ```powershell
  # Requires sqlite3.exe on PATH
  sqlite3 "C:\workspace\web\reddit-to-bsky\out\posted.$ts.db.bak" "PRAGMA integrity_check;"
  ```

---

## 5 · Archive Logs

- [ ] [OPTIONAL] Compress current log file:
  ```powershell
  $ts = Get-Date -Format "yyyyMMdd_HHmmss"
  Compress-Archive -Path "C:\workspace\web\reddit-to-bsky\logs\bot.log" `
                   -DestinationPath "C:\workspace\web\reddit-to-bsky\logs\bot.$ts.zip"
  Remove-Item "C:\workspace\web\reddit-to-bsky\logs\bot.log"
  ```

---

## 6 · Clean Up Temp Files

- [ ] Remove any leftover downloaded images from the temp/cache folder:
  ```powershell
  Remove-Item "C:\workspace\web\reddit-to-bsky\out\*.jpg" -ErrorAction SilentlyContinue
  Remove-Item "C:\workspace\web\reddit-to-bsky\out\*.png" -ErrorAction SilentlyContinue
  Remove-Item "C:\workspace\web\reddit-to-bsky\out\*.webp" -ErrorAction SilentlyContinue
  ```

---

## 7 · Secure Credentials (Decommission Only)

> Skip this section if you're only pausing temporarily.

- [ ] Remove user-scoped environment variables:
  ```powershell
  [Environment]::SetEnvironmentVariable("BLUESKY_HANDLE",       $null, "User")
  [Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", $null, "User")
  ```
- [ ] Revoke the Bluesky app password:
  1. Go to https://bsky.app → **Settings → App Passwords**
  2. Delete the password used by this bot
- [ ] Clear `appsettings.json` credentials (replace with placeholders):
  ```json
  "BlueskyConfig": {
    "Handle":      "",
    "AppPassword": ""
  }
  ```

---

## 8 · Remove the Scheduled Task (Decommission Only)

- [ ] Unregister the task completely:
  ```powershell
  Unregister-ScheduledTask -TaskName "RedditToBsky" -Confirm:$false
  ```

---

## 9 · Final Confirmation

- [ ] No process running: `Get-Process -Name "reddit-to-bsky"` returns nothing
- [ ] Task disabled/removed: `Get-ScheduledTask -TaskName "RedditToBsky"` shows `Disabled` or not found
- [ ] Database backed up
- [ ] Credentials cleared (if decommissioning)

---

## Re-enable After a Pause

```powershell
# Re-enable without re-registering
Enable-ScheduledTask -TaskName "RedditToBsky"
Get-ScheduledTask -TaskName "RedditToBsky" | Select-Object State, NextRunTime
```

To redeploy from scratch, follow [DEPLOYMENT.md](DEPLOYMENT.md).
