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

    static BlueskyClient()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", "RedditToBlueskyBot/1.0");
    }

    public static async Task<bool> PostAsync(string text, string imagePath)
    {
        try
        {
            // Ensure logged in
            if (string.IsNullOrEmpty(_sessionToken))
            {
                bool loggedIn = await LoginAsync();
                if (!loggedIn)
                {
                    Logger.Error("Failed to authenticate with Bluesky");
                    return false;
                }
            }

            // Upload image as blob
            string? blobCid = await UploadImageBlobAsync(imagePath);
            if (string.IsNullOrEmpty(blobCid))
            {
                Logger.Error("Failed to upload image to Bluesky");
                return false;
            }

            // Create post with text and embedded image
            return await CreatePostAsync(text, blobCid);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error posting to Bluesky");
            return false;
        }
    }

    private static async Task<bool> LoginAsync()
    {
        try
        {
            // Load credentials from appsettings.json or environment
            string handle = Environment.GetEnvironmentVariable("BLUESKY_HANDLE") ?? string.Empty;
            string appPassword = Environment.GetEnvironmentVariable("BLUESKY_APP_PASSWORD") ?? string.Empty;

            if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(appPassword))
            {
                Logger.Error("Bluesky credentials not set (BLUESKY_HANDLE, BLUESKY_APP_PASSWORD)");
                return false;
            }

            // Normalize handle formats: accept 'user', 'user.bsky.social', or 'user@bsky.social'
            string normalizedHandle = handle.Trim();
            if (normalizedHandle.Contains("@"))
            {
                normalizedHandle = normalizedHandle.Replace("@", ".");
            }
            if (!normalizedHandle.Contains("."))
            {
                // assume bare username, append domain
                normalizedHandle = normalizedHandle + ".bsky.social";
            }

            Logger.Debug($"Using Bluesky handle: {normalizedHandle}");

            var loginPayload = new
            {
                identifier = normalizedHandle,
                password = appPassword
            };

            var content = new StringContent(
                JsonSerializer.Serialize(loginPayload),
                Encoding.UTF8,
                "application/json");

            var response = await Client.PostAsync($"{BlueskyApiUrl}/com.atproto.server.createSession", content);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                Logger.Error($"Bluesky login failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                Logger.Debug($"Bluesky response body: {body}");
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

    private static async Task<string?> UploadImageBlobAsync(string imagePath)
    {
        try
        {
            using var fs = File.OpenRead(imagePath);
            var content = new MultipartFormDataContent();
            content.Add(new StreamContent(fs), "file", Path.GetFileName(imagePath));

            var response = await Client.PostAsync($"{BlueskyApiUrl}/com.atproto.repo.uploadBlob", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var cid = root.GetProperty("cid").GetString();
            return cid;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error uploading image to Bluesky");
            return null;
        }
    }

    private static async Task<bool> CreatePostAsync(string text, string blobCid)
    {
        try
        {
            // Build payload using dictionaries so we can include properties like "$type"
            var imageObj = new Dictionary<string, object> { ["image"] = blobCid };
            var embedObj = new Dictionary<string, object>
            {
                ["$type"] = "app.bsky.embed.images",
                ["images"] = new[] { imageObj }
            };

            var recordObj = new Dictionary<string, object>
            {
                ["text"] = text,
                ["embed"] = embedObj
            };

            var createRecord = new Dictionary<string, object>
            {
                ["repo"] = _did,
                ["collection"] = "app.bsky.feed.post",
                ["record"] = recordObj
            };

            var content = new StringContent(JsonSerializer.Serialize(createRecord), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync($"{BlueskyApiUrl}/com.atproto.repo.createRecord", content);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating Bluesky post");
            return false;
        }
    }
}
