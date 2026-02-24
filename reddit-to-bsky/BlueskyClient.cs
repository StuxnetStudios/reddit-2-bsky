using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using NLog;

public static class BlueskyClient
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private static readonly HttpClient Client = new HttpClient();
    private const string BlueskyApiUrl = "https://bsky.social/xrpc";
    private static string? _sessionToken;
    private static string? _did;
    // Track if login already failed this run so we don't hammer the API
    private static bool _loginFailed = false;

    static BlueskyClient()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", "RedditToBlueskyBot/1.0");
    }

    public static async Task<bool> PostAsync(string text, string imagePath)
    {
        try
        {
            // Ensure logged in — only attempt once per run
            if (string.IsNullOrEmpty(_sessionToken))
            {
                if (_loginFailed)
                {
                    Logger.Error("Skipping post — login already failed this run");
                    return false;
                }
                bool loggedIn = await LoginAsync();
                if (!loggedIn)
                {
                    _loginFailed = true;
                    Logger.Error("Failed to authenticate with Bluesky");
                    return false;
                }
            }

            // Upload image as blob
            BlobRef? blob = await UploadImageBlobAsync(imagePath);
            if (blob == null)
            {
                Logger.Error("Failed to upload image to Bluesky");
                return false;
            }

            // Create post with text and embedded image
            return await CreatePostAsync(text, blob);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error posting to Bluesky");
            return false;
        }
    }

    private static string LoadCredential(string envVar, string settingsKey)
    {
        // Prefer environment variable, fall back to appsettings.json
        string? value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(value)) return value;

        try
        {
            string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath))
                settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (File.Exists(settingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty("BlueskyConfig", out var section) &&
                    section.TryGetProperty(settingsKey, out var val))
                    return val.GetString() ?? string.Empty;
            }
        }
        catch { }

        return string.Empty;
    }

    private static async Task<bool> LoginAsync()
    {
        try
        {
            string handle = LoadCredential("BLUESKY_HANDLE", "Handle");
            string appPassword = LoadCredential("BLUESKY_APP_PASSWORD", "AppPassword");

            if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(appPassword))
            {
                Logger.Error("Bluesky credentials not set (BLUESKY_HANDLE / BLUESKY_APP_PASSWORD)");
                return false;
            }

            // Normalize: accept 'user', 'user.bsky.social', or 'user@bsky.social'
            // AT Protocol identifier uses the dot form: user.bsky.social
            string normalizedHandle = handle.Trim();
            if (normalizedHandle.Contains("@"))
                normalizedHandle = normalizedHandle.Replace("@", ".");
            if (!normalizedHandle.Contains("."))
                normalizedHandle = normalizedHandle + ".bsky.social";

            Logger.Debug($"Using Bluesky handle: {normalizedHandle}");

            var loginPayload = new { identifier = normalizedHandle, password = appPassword };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync($"{BlueskyApiUrl}/com.atproto.server.createSession", content);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                Logger.Error($"Bluesky login failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                Logger.Debug($"Bluesky response body: {body}");

                if ((int)response.StatusCode == 429)
                {
                    double? retrySeconds = null;
                    if (response.Headers.RetryAfter?.Delta != null)
                        retrySeconds = response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                    else if (response.Headers.RetryAfter?.Date != null)
                        retrySeconds = (response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;

                    DateTime next = (retrySeconds.HasValue && retrySeconds.Value > 0)
                        ? DateTime.UtcNow.AddSeconds(retrySeconds.Value)
                        : DateTime.UtcNow.AddMinutes(15);

                    Database.SetNextAllowedPostUtc(next);
                    Logger.Info($"Rate limited — next allowed post UTC: {next:o}");
                }
                return false;
            }

            string json = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                _sessionToken = root.GetProperty("accessJwt").GetString();
                _did = root.GetProperty("did").GetString();
            }

            Client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sessionToken);

            Logger.Info($"Logged in to Bluesky as {normalizedHandle}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error logging in to Bluesky");
            return false;
        }
    }

    // Holds the blob reference returned by uploadBlob
    private record BlobRef(string Link, string MimeType, long Size);

    private static async Task<BlobRef?> UploadImageBlobAsync(string imagePath)
    {
        try
        {
            // Determine MIME type from extension
            string ext = Path.GetExtension(imagePath).ToLowerInvariant();
            string mimeType = ext switch
            {
                ".png"  => "image/png",
                ".gif"  => "image/gif",
                ".webp" => "image/webp",
                _       => "image/jpeg"
            };

            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);

            // Bluesky uploadBlob expects raw binary body, NOT multipart
            var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

            var response = await Client.PostAsync($"{BlueskyApiUrl}/com.atproto.repo.uploadBlob", content);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                Logger.Error($"Blob upload failed: {(int)response.StatusCode} — {body}");
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var blob = doc.RootElement.GetProperty("blob");
            string link = blob.GetProperty("ref").GetProperty("$link").GetString() ?? string.Empty;
            string blobMime = blob.GetProperty("mimeType").GetString() ?? mimeType;
            long size = blob.GetProperty("size").GetInt64();
            return new BlobRef(link, blobMime, size);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error uploading image to Bluesky");
            return null;
        }
    }

    private static async Task<bool> CreatePostAsync(string text, BlobRef blob)
    {
        try
        {
            // Proper AT Protocol blob ref structure
            var blobObj = new Dictionary<string, object>
            {
                ["$type"]    = "blob",
                ["ref"]      = new Dictionary<string, object> { ["$link"] = blob.Link },
                ["mimeType"] = blob.MimeType,
                ["size"]     = blob.Size
            };

            var imageObj = new Dictionary<string, object>
            {
                ["image"] = blobObj,
                ["alt"]   = ""
            };

            var embedObj = new Dictionary<string, object>
            {
                ["$type"]  = "app.bsky.embed.images",
                ["images"] = new[] { imageObj }
            };

            var recordObj = new Dictionary<string, object>
            {
                ["$type"]     = "app.bsky.feed.post",
                ["text"]      = TruncateToGraphemes(text, 300),
                ["embed"]     = embedObj,
                ["createdAt"] = DateTime.UtcNow.ToString("o")
            };

            var createRecord = new Dictionary<string, object>
            {
                ["repo"]       = _did!,
                ["collection"] = "app.bsky.feed.post",
                ["record"]     = recordObj
            };

            var content = new StringContent(JsonSerializer.Serialize(createRecord), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync($"{BlueskyApiUrl}/com.atproto.repo.createRecord", content);

            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"createRecord failed: {(int)response.StatusCode} — {body}");
                return false;
            }

            // log the response body on success to get the record URI
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("uri", out var uriProp))
                {
                    Logger.Info($"Bluesky post created: {uriProp.GetString()}");
                }
            }
            catch { }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating Bluesky post");
            return false;
        }
    }

    private static string TruncateToGraphemes(string text, int maxGraphemes)
    {
        var elements = new List<string>();
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            elements.Add(enumerator.GetTextElement());

        if (elements.Count <= maxGraphemes)
            return text;

        // Truncate to maxGraphemes-1 and append ellipsis
        return string.Concat(elements.Take(maxGraphemes - 1)) + "…";
    }
}
