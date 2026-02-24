using NLog;

public static class DbInit
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public static void Initialize()
    {
        _logger.Debug("Attempting to initialize database...");
        try
        {
            using (var conn = Database.GetConnection())
            {
                _logger.Debug("Creating tables if not exist...");
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS posted (
                        reddit_id TEXT PRIMARY KEY,
                        img_hash TEXT,
                        posted_at DATETIME
                    );
                    CREATE TABLE IF NOT EXISTS metadata (
                        key TEXT PRIMARY KEY,
                        value TEXT
                    );
                ";
                cmd.ExecuteNonQuery();
                _logger.Debug("Tables created/verified successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error initializing database");
            _logger.Error(ex);
            throw;
        }
    }
}
