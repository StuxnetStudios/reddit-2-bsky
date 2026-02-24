I want you to build a complete Windows‑11‑compatible automation app that performs the following workflow:

GOAL:
Scrape r/Conservative for posts with more than 200 upvotes that contain an image, extract the top comment, deduplicate images using perceptual hashing, and post the image + top comment to Bluesky using my bot credentials. The app must run locally on Windows 11.

IMPORTANT:
Reddit API access is NOT available. You must use Pushshift + HTML scraping instead of Reddit’s API.

-------------------------------------
ARCHITECTURE REQUIREMENTS
-------------------------------------

1. **Data Source (Reddit)**
   - Use Pushshift API:
     https://api.pullpush.io/reddit/search/submission/
   - Query: subreddit=conservative, score=>200, size=50
   - Filter only posts where the URL ends with .jpg/.jpeg/.png
   - For each post, scrape the Reddit permalink HTML to extract the top comment.
   - Use BeautifulSoup for HTML parsing.

2. **Image Handling**
   - Download the image locally.
   - Compute a perceptual hash (pHash) using `imagehash` + Pillow.
   - Store the hash in a SQLite database.
   - Before posting, compare the hash against stored hashes to avoid duplicates.                           2 - If the hash already exists, skip the post.



3. **Database**
   - SQLite database named `posted.db`
   - Table: posted(reddit_id TEXT PRIMARY KEY, img_hash TEXT)
   - Functions needed:
     - already_posted(reddit_id)
     - is_duplicate_image(img_hash)
     - mark_posted(reddit_id, img_hash)

4. **Bluesky Posting**
   - Use the `atproto` Python library.
   - Login using handle + app password.
   - Upload the image as a blob.
   - Create a post where:
       text = top comment
       embed = uploaded image

5. **Logging**
   - Log all actions to `bot.log`
   - Log:
     - posts skipped (duplicate)
     - posts successfully uploaded
     - errors

6. **Windows 11 Compatibility**
   - Script must run with Python 3.10+ on Windows 11.
   - Provide instructions for:
     - Installing dependencies via PowerShell
     - Running the script manually
     - Setting up Windows Task Scheduler to run hourly
   - Provide optional instructions for packaging into a standalone EXE using PyInstaller.

7. **Final Deliverables**
   - Full C# project (reddit_to_bsky.cs)
   - Database initialization script (db_init.py)
   - Database upgrade script to add img_hash column (db_upgrade.py)
   - Requirements list
   - Step-by-step Windows installation instructions
   - Task Scheduler setup instructions
   - Optional: PyInstaller packaging instructions

-------------------------------------
STYLE REQUIREMENTS
-------------------------------------

- Code must be clean, well‑commented, and production‑ready.
- No placeholders — use clear variable names and explain where credentials go.
- Include error handling for:
  - network failures
  - missing comments
  - invalid images
  - Bluesky upload failures
- The final answer must be a complete, ready‑to‑run solution.

-------------------------------------
BEGIN