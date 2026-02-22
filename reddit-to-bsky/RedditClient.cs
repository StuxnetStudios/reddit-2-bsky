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
    private const int MinScore = 200;
    private static List<string> _subreddits = new();

    static RedditClient()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", "RedditToBlueskyBot/1.0");
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
            string queryUrl = $"{PushshiftApiUrl}?subreddit={subreddit}&score=>{MinScore}&limit=100&has_url=true";
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
                                // Fetch top comment
                                post.TopComment = await FetchTopCommentAsync(post.RedditId);
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

    private static async Task<string> FetchTopCommentAsync(string postId)
    {
        try
        {
            string permalinkUrl = $"https://reddit.com/comments/{postId}";
            var response = await Client.GetAsync(permalinkUrl);
            response.EnsureSuccessStatusCode();

            string html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // XPath to select top comment (stub - adjust based on Reddit HTML structure)
            var commentNode = doc.DocumentNode.SelectSingleNode("//div[@class='Comment']");
            if (commentNode != null)
            {
                return commentNode.InnerText.Trim();
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, $"Error fetching top comment for post {postId}");
        }

        return "No comment available";
    }
}
