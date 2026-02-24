using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using HtmlAgilityPack;
using NLog;

public class RedditPost
{
    public string RedditId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string TopComment { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Subreddit { get; set; } = string.Empty;
}

public static class RedditClient
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private static readonly HttpClient Client = new HttpClient();
    private const string PushshiftApiUrl = "https://api.pullpush.io/reddit/search/submission/";
    private static int MinScore = 200;        // default, overridden by config
    private static int FetchLimit = 100;       // default, overridden by config
    private static List<string> _subreddits = new();

    static RedditClient()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", "RedditToBlueskyBot/1.0");
        LoadConfig();
    }

    private static void LoadConfig()
    {
        try
        {
            string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath))
                settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (File.Exists(settingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty("RedditConfig", out var config))
                {
                    if (config.TryGetProperty("MinimumScore", out var min) && min.TryGetInt32(out var mi))
                        MinScore = mi;
                    if (config.TryGetProperty("FetchLimit", out var lim) && lim.TryGetInt32(out var fl))
                        FetchLimit = fl;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Error loading Reddit config");
        }
    }

    public static void SetSubreddits(List<string> subreddits)
    {
        _subreddits = subreddits ?? new List<string> { "Conservative" };
        if (_subreddits.Count == 0)
            _subreddits.Add("Conservative");
        
        Logger.Info($"Configured subreddits: {string.Join(", ", _subreddits)}");
    }

    public static async Task<List<RedditPost>> FetchPostsAsync()
    {
        var allPosts = new List<RedditPost>();

        if (_subreddits.Count == 0)
            SetSubreddits(new List<string> { "Conservative" });

        Logger.Info($"Reddit config: minScore={MinScore}, fetchLimit={FetchLimit}");

        // Fetch from each subreddit
        foreach (var subreddit in _subreddits)
        {
            try
            {
                Logger.Info($"Fetching posts from r/{subreddit}...");
                var posts = await FetchFromSubredditAsync(subreddit);
                allPosts.AddRange(posts);
                Logger.Info($"Fetched {posts.Count} posts from r/{subreddit}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error fetching from r/{subreddit}");
            }
        }

        return allPosts;
    }

    private static async Task<List<RedditPost>> FetchFromSubredditAsync(string subreddit)
    {
        var posts = new List<RedditPost>();

        try
        {
            // Query Pushshift API
            string queryUrl = $"{PushshiftApiUrl}?subreddit={subreddit}&score=>{MinScore}&limit={FetchLimit}&has_url=true";
            Logger.Debug($"Querying: {queryUrl}");

            var response = await Client.GetAsync(queryUrl);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var dataArray))
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        try
                        {
                            var post = ParseRedditPost(item, subreddit);
                            if (post != null && IsValidImageUrl(post.ImageUrl))
                            {
                                posts.Add(post);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug(ex, "Error parsing Reddit post");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error fetching posts from r/{subreddit}");
        }

        return posts;
    }

    private static RedditPost? ParseRedditPost(JsonElement item, string subreddit)
    {
        if (!item.TryGetProperty("id", out var id) ||
            !item.TryGetProperty("title", out var title) ||
            !item.TryGetProperty("url", out var url) ||
            !item.TryGetProperty("score", out var score))
        {
            return null;
        }

        return new RedditPost
        {
            RedditId = id.GetString() ?? string.Empty,
            Title = title.GetString() ?? string.Empty,
            ImageUrl = url.GetString() ?? string.Empty,
            Score = score.GetInt32(),
            Subreddit = subreddit
        };
    }

    private static bool IsValidImageUrl(string url)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png" };
        foreach (var ext in extensions)
        {
            if (url.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static async Task<string> FetchTopCommentAsync(string postId)
    {
        try
        {
            string jsonUrl = $"https://www.reddit.com/comments/{postId}.json?limit=10&sort=top";
            var request = new HttpRequestMessage(HttpMethod.Get, jsonUrl);
            request.Headers.Add("User-Agent", "RedditToBlueskyBot/1.0");
            var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // Response is an array: [post_listing, comments_listing]
            var commentsListing = doc.RootElement[1];
            var children = commentsListing
                .GetProperty("data")
                .GetProperty("children");

            foreach (var child in children.EnumerateArray())
            {
                var kind = child.GetProperty("kind").GetString();
                if (kind != "t1") continue; // t1 = comment

                var data = child.GetProperty("data");

                // Skip removed/deleted/AutoModerator/mod comments
                var author = data.TryGetProperty("author", out var a) ? a.GetString() : null;
                var body = data.TryGetProperty("body", out var b) ? b.GetString() : null;
                if (string.IsNullOrWhiteSpace(body) || body == "[removed]" || body == "[deleted]")
                    continue;
                if (author == "AutoModerator")
                    continue;

                // Skip stickied comments (usually mod notices)
                if (data.TryGetProperty("stickied", out var stickied) && stickied.GetBoolean())
                    continue;

                // Skip mod-distinguished comments
                if (data.TryGetProperty("distinguished", out var distinguished) &&
                    distinguished.ValueKind != JsonValueKind.Null &&
                    distinguished.GetString() == "moderator")
                    continue;

                // Skip comments that look like mod removal notices
                var lowerBody = body.ToLowerInvariant();
                if (lowerBody.Contains("your submission was removed") ||
                    lowerBody.Contains("your post was removed") ||
                    lowerBody.Contains("removed for the following reason") ||
                    lowerBody.Contains("please read the rules") ||
                    lowerBody.Contains("rule violation"))
                    continue;

                return body.Trim();
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Error fetching top comment for post {postId}: {ex.Message}");
        }

        return string.Empty;
    }
}
