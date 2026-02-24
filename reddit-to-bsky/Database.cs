using System;
using Microsoft.Data.Sqlite;
using NLog;

public static class Database
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "posted.db");
    private static SqliteConnection? _connection;

    static Database()
    {
        _logger.Debug($"Database path: {DbPath}");
    }

    public static SqliteConnection GetConnection()
    {
        try
        {
            _logger.Debug($"Getting database connection...");
            _logger.Debug($"Database path: {DbPath}");
            
            string connectionString = $"Data Source={DbPath}";
            _logger.Debug($"Connection string: {connectionString}");
            
            _logger.Debug($"Creating new SQLite connection...");
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            _logger.Debug($"Connection opened successfully");
            return connection;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create/open SQLite connection at {DbPath}");
            _logger.Error(ex);
            throw;
        }
    }

    public static bool AlreadyPosted(string redditId)
    {
        _logger.Debug($"Checking if already posted: {redditId}");
        using (var conn = GetConnection())
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM posted WHERE reddit_id = @id";
            cmd.Parameters.AddWithValue("@id", redditId);
            var result = (long?)cmd.ExecuteScalar();
            return result > 0;
        }
    }

    public static bool IsDuplicateImage(string imageHash)
    {
        _logger.Debug($"Checking if duplicate image: {imageHash}");
        using (var conn = GetConnection())
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM posted WHERE img_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", imageHash);
            var result = (long?)cmd.ExecuteScalar();
            return result > 0;
        }
    }

    public static void MarkPosted(string redditId, string imageHash)
    {
        _logger.Debug($"Marking as posted: {redditId} with hash {imageHash}");
        using (var conn = GetConnection())
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO posted (reddit_id, img_hash, posted_at)
                                VALUES (@id, @hash, @now)
                                ON CONFLICT(reddit_id) DO UPDATE SET
                                  img_hash = @hash,
                                  posted_at = @now";
            cmd.Parameters.AddWithValue("@id", redditId);
            cmd.Parameters.AddWithValue("@hash", imageHash);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            cmd.ExecuteNonQuery();
            _logger.Debug($"Successfully marked as posted");
        }
    }

    public static string? GetMetadata(string key)
    {
        using (var conn = GetConnection())
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM metadata WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var result = cmd.ExecuteScalar();
            return result == null ? null : (string)result;
        }
    }

    public static void SetMetadata(string key, string? value)
    {
        using (var conn = GetConnection())
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO metadata (key,value) VALUES (@key,@value)
                                ON CONFLICT(key) DO UPDATE SET value = @value";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public static DateTime? GetNextAllowedPostUtc()
    {
        var val = GetMetadata("next_allowed_post_utc");
        if (string.IsNullOrEmpty(val)) return null;
        if (DateTime.TryParse(val, out var dt)) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }

    public static void SetNextAllowedPostUtc(DateTime? dt)
    {
        if (dt == null)
            SetMetadata("next_allowed_post_utc", null);
        else
            SetMetadata("next_allowed_post_utc", dt.Value.ToString("o"));
    }

    public static void Close()
    {
        _connection?.Dispose();
        _connection = null;
        _logger.Debug("Database connection closed");
    }
}
