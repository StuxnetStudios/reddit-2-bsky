using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using NLog;

class Program
{
    // Renamed to avoid conflict with Logger class
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    static async Task Main(string[] args)
    {
        // Initialize logging FIRST before any log calls
        Logger.Setup();
        
        _logger.Info("========== Reddit to Bluesky Bot Started ==========");

        try
        {
            // Initialize database
            DbInit.Initialize();
            DbUpgrade.MigrateIfNeeded();
            // Check persistent cooldown for rate limits
            var nextAllowed = Database.GetNextAllowedPostUtc();
            if (nextAllowed.HasValue && nextAllowed.Value > DateTime.UtcNow)
            {
                _logger.Warn($"Next allowed post time is {nextAllowed.Value:o} UTC; exiting to avoid rate limits");
                return;
            }

            // Load configuration
            var subreddits = LoadSubredditsFromConfig();
            _logger.Info($"Available subreddits: {string.Join(", r/", subreddits)}");

            // Parse -n <count> limit from args
            int maxPosts = int.MaxValue;
            var filteredArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-n" && i + 1 < args.Length && int.TryParse(args[i + 1], out int n))
                {
                    maxPosts = n;
                    i++; // skip the number
                }
                else
                {
                    filteredArgs.Add(args[i]);
                }
            }
            args = filteredArgs.ToArray();
            if (maxPosts != int.MaxValue)
                _logger.Info($"Post limit: {maxPosts}");

            // If no command line args, prompt user for subreddit selection
            List<string> selectedSubreddits = subreddits;
            if (args.Length == 0)
            {
                selectedSubreddits = PromptForSubreddits(subreddits);
            }
            else if (args.Length >= 1 && args[0].ToLower() == "-a")
            {
                // -a flag means use all subreddits
                _logger.Info("Using all configured subreddits");
            }
            else
            {
                // Filter to specified subreddits
                selectedSubreddits = new List<string>();
                foreach (var arg in args)
                {
                    string sr = arg.StartsWith("r/") ? arg.Substring(2) : arg;
                    if (subreddits.Contains(sr, StringComparer.OrdinalIgnoreCase))
                    {
                        selectedSubreddits.Add(sr);
                    }
                }
                if (selectedSubreddits.Count == 0)
                    selectedSubreddits = subreddits;
            }

            // Configure Reddit client
            RedditClient.SetSubreddits(selectedSubreddits);

            _logger.Info("Starting Reddit scrape cycle...");

            // Scrape Reddit
            var posts = await RedditClient.FetchPostsAsync();
            _logger.Info($"Total posts fetched: {posts.Count}");

            // Process each post
            int successCount = 0;
            int skipCount = 0;

            foreach (var post in posts)
            {
                try
                {
                    // Check if already posted
                    if (Database.AlreadyPosted(post.RedditId))
                    {
                        _logger.Debug($"Skipping r/{post.Subreddit} post {post.RedditId} - already posted");
                        skipCount++;
                        continue;
                    }

                    // Download and hash image
                    string? imagePath = await ImageUtils.DownloadImageAsync(post.ImageUrl);
                    if (imagePath == null)
                    {
                        _logger.Warn($"Failed to download image for r/{post.Subreddit} post {post.RedditId}");
                        skipCount++;
                        continue;
                    }

                    string imageHash = ImageUtils.ComputePerceptualHash(imagePath);

                    // Check for duplicate image
                    if (Database.IsDuplicateImage(imageHash))
                    {
                        _logger.Debug($"Skipping r/{post.Subreddit} post {post.RedditId} - duplicate image");
                        File.Delete(imagePath);
                        skipCount++;
                        continue;
                    }

                    // Post to Bluesky — use top comment, fall back to post title
                    string topComment = await RedditClient.FetchTopCommentAsync(post.RedditId);
                    string postText = !string.IsNullOrWhiteSpace(topComment)
                        ? topComment
                        : post.Title;
                    bool success = await BlueskyClient.PostAsync(postText, imagePath);
                    if (success)
                    {
                        Database.MarkPosted(post.RedditId, imageHash);
                        _logger.Info($"Posted r/{post.Subreddit}/{post.RedditId} to Bluesky");
                        successCount++;
                        if (successCount >= maxPosts)
                        {
                            _logger.Info($"Reached post limit of {maxPosts}, stopping.");
                            break;
                        }
                    }
                    else
                    {
                        _logger.Error($"Failed to post r/{post.Subreddit}/{post.RedditId} to Bluesky");
                    }

                    File.Delete(imagePath);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Error processing post {post.RedditId}");
                }
            }

            _logger.Info($"========== Cycle Complete: {successCount} posted, {skipCount} skipped ==========");
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Fatal error in main loop");
            Environment.Exit(1);
        }
    }

    private static List<string> LoadSubredditsFromConfig()
    {
        try
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
            {
                _logger.Warn("appsettings.json not found, using default subreddits");
                return new List<string> { "Conservative" };
            }

            string json = File.ReadAllText(configPath);
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("RedditConfig", out var config) &&
                    config.TryGetProperty("Subreddits", out var subredditsArray))
                {
                    var result = new List<string>();
                    foreach (var item in subredditsArray.EnumerateArray())
                    {
                        var sr = item.GetString();
                        if (!string.IsNullOrWhiteSpace(sr))
                            result.Add(sr);
                    }
                    return result.Count > 0 ? result : new List<string> { "Conservative" };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading subreddits from config");
        }

        return new List<string> { "ProgrammerHumor" };
    }

    private static List<string> PromptForSubreddits(List<string> available)
    {
        Console.WriteLine();
        Console.WriteLine("========== Subreddit Selection ==========");
        Console.WriteLine();
        Console.WriteLine("Available subreddits:");

        for (int i = 0; i < available.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. r/{available[i]}");
        }

        Console.WriteLine($"  A. All ({available.Count} subreddits)");
        Console.WriteLine();
        Console.Write("Select subreddits (comma-separated numbers, or 'A' for all): ");

        string? input = Console.ReadLine()?.Trim().ToUpper();

        if (string.IsNullOrWhiteSpace(input) || input == "A")
        {
            Console.WriteLine($"Selected: All subreddits");
            return available;
        }

        var selected = new List<string>();
        foreach (var part in input.Split(','))
        {
            if (int.TryParse(part.Trim(), out int idx) && idx >= 1 && idx <= available.Count)
            {
                selected.Add(available[idx - 1]);
            }
        }

        if (selected.Count == 0)
        {
            Console.WriteLine("No valid selection, using all subreddits");
            return available;
        }

        Console.WriteLine($"Selected: {string.Join(", r/", selected)}");
        return selected;
    }
}
