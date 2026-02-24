# Reddit to Bluesky Bot

**Version 1.0**

A Windows 11 automation app that scrapes multiple subreddits, deduplicates images using perceptual hashing, and posts to Bluesky.

## Features

- Multi-subreddit support (configure multiple subreddits in appsettings.json)
- Interactive subreddit selection at startup
- Reddit scraping via Pushshift API (posts with score > 200)
- HTML comment extraction with HtmlAgilityPack
- Image deduplication using perceptual hashing (8x8 pHash)
- SQLite tracking database (reddit_id + img_hash)
- Bluesky integration with atproto protocol
- NLog file and console logging with daily rotation
- Windows Task Scheduler automation
- Single-file executable (34.7 MB, includes .NET runtime)

## Prerequisites

- Windows 11
- .NET 8.0 SDK (for building) or just the EXE (for running)
- Bluesky account with app password

## Quick Start

### Step 1: Set Up Bluesky Credentials

See [CREDENTIALS.md](CREDENTIALS.md) for detailed instructions on storing credentials securely using environment variables.

Quick version:
```powershell
[Environment]::SetEnvironmentVariable("BLUESKY_HANDLE", "stux-buddy@bsky.social", "User")
[Environment]::SetEnvironmentVariable("BLUESKY_APP_PASSWORD", "bsky-api", "User")
```

### Step 2: Run the Bot

```bash
cd c:\workspace\web\reddit-to-bsky\out
reddit-to-bsky.exe
```

The app will prompt you to select which subreddits to process.

## Configuration

### Subreddits

Edit `appsettings.json` to add or modify subreddits:

```json
{
  "RedditConfig": {
    "Subreddits": [
      "Conservative",
      "Republican", 
      "politics",
      "news",
      "worldnews"
    ],
    "MinimumScore": 200,
    "FetchLimit": 100
  }
}
```

### Command-Line Usage

```bash
# Interactive subreddit selection
reddit-to-bsky.exe

# Process all configured subreddits
reddit-to-bsky.exe -a

# Process specific subreddits
reddit-to-bsky.exe r/Conservative r/Republican
```

## Credentials Management

**See [CREDENTIALS.md](CREDENTIALS.md) for complete instructions.**

Credentials are stored as Windows environment variables:
- `BLUESKY_HANDLE` - Your Bluesky handle
- `BLUESKY_APP_PASSWORD` - Your Bluesky app password

This approach:
- Keeps credentials out of code and config files
- Uses Windows Registry encryption
- Works with Task Scheduler
- Survives app updates

## Windows Task Scheduler

Run the automated setup script as Administrator:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
C:\workspace\web\reddit-to-bsky\setup-task-scheduler.ps1
```

This creates an hourly scheduled task that runs `-a` (all subreddits).

To run specific subreddits on a schedule, edit the task manually in Task Scheduler to append arguments like: `r/Conservative r/Republican`

## Release Notes

### v1.0 (2026-02-24)

- Initial public release
- Features multi-subreddit scraping, image deduplication, top-comment posting, and 45‑minute cooldown
- Configurable thresholds and fetch limits
- Windows Task Scheduler integration and credential docs

## Database Schema

```sql
CREATE TABLE posted (
    reddit_id TEXT PRIMARY KEY,
    img_hash TEXT,
    posted_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

- `reddit_id`: Unique Reddit post identifier
- `img_hash`: Hex-encoded perceptual hash (8x8 grid)
- `posted_at`: Timestamp of post

## Logging

- **Console:** Real-time output during execution
- **File:** `bot.log` in app directory
- **Rotation:** Daily, keeps max 10 archives

Log levels: Debug, Info, Warn, Error, Fatal

## Project Structure

```
reddit-to-bsky/
├── Program.cs              # Main orchestration + subreddit selection
├── RedditClient.cs         # Multi-subreddit Pushshift API 
├── ImageUtils.cs           # Download + perceptual hash
├── BlueskyClient.cs        # atproto API wrapper
├── Database.cs             # SQLite operations
├── Logger.cs               # NLog setup
├── DbInit.cs               # Schema creation
├── DbUpgrade.cs            # Migrations
├── reddit-to-bsky.csproj   # Project file
├── appsettings.json        # Configuration
├── CREDENTIALS.md          # Bluesky credentials setup
├── DEPLOYMENT.md           # Deployment guide
├── out/
│   └── reddit-to-bsky.exe  # Standalone executable
├── posted.db               # Runtime database
└── bot.log                 # Runtime logs
```

## Troubleshooting

### "Bluesky credentials not set"

See [CREDENTIALS.md - Troubleshooting](CREDENTIALS.md#troubleshooting)

Verify environment variables are set:
```powershell
$env:BLUESKY_HANDLE
$env:BLUESKY_APP_PASSWORD
```

### "No subreddits selected"

Make sure `appsettings.json` exists with populated `Subreddits` array.

### Database locked

Only one instance can run at a time. Check Task Scheduler if running as scheduled task.

### No posts fetched

- Bluesky rate limits API requests; try again later
- Check if subreddit has posts with score > 200 and image URLs
- Review `bot.log` for API errors

## Documentation Files

- [README.md](README.md) - This file (overview and usage)
- [CREDENTIALS.md](CREDENTIALS.md) - Bluesky account setup (environment variables)
- [DEPLOYMENT.md](DEPLOYMENT.md) - Windows Task Scheduler deployment

## License

This project is provided as-is for Windows 11 automation.
