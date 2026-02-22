using NLog;

public static class DbUpgrade
{
    private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public static void MigrateIfNeeded()
    {
        _logger.Debug("Checking for database schema upgrades...");
        try
        {
            using (var conn = Database.GetConnection())
            {
                // Check if img_hash column exists
                var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(posted)";
                using (var reader = cmd.ExecuteReader())
                {
                    bool hasImageHash = false;
                    while (reader.Read())
                    {
                        if (reader[1].ToString() == "img_hash")
                        {
                            hasImageHash = true;
                            break;
                        }
                    }

                    if (!hasImageHash)
                    {
                        _logger.Info("Adding img_hash column to posted table...");
                        var alterCmd = conn.CreateCommand();
                        alterCmd.CommandText = "ALTER TABLE posted ADD COLUMN img_hash TEXT";
                        alterCmd.ExecuteNonQuery();
                        _logger.Info("Successfully added img_hash column");
                    }
                    else
                    {
                        _logger.Debug("img_hash column already exists");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error during database migration");
            _logger.Error(ex);
            throw;
        }
    }
}
