using System;
using NLog;
using NLog.Config;
using NLog.Targets;

public static class Logger
{
    public static void Setup()
    {
        var config = new LoggingConfiguration();

        // Console target - now includes exception details
        var consoleTarget = new ConsoleTarget
        {
            Name = "console",
            Layout = @"${date:format=yyyy-MM-dd HH:mm:ss} [${level:uppercase=true}] ${message}${onexception:${newline}${exception:format=tostring}}"
        };
        config.AddTarget("console", consoleTarget);

        // File target
        var fileTarget = new FileTarget
        {
            Name = "file",
            FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bot.log"),
            Layout = @"${date:format=yyyy-MM-dd HH:mm:ss} [${level:uppercase=true}] ${message}${onexception:${newline}${exception:format=tostring}}",
            ArchiveFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bot.{#}.log"),
            ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 10
        };
        config.AddTarget("file", fileTarget);

        // Rules
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, "console");
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, "file");

        LogManager.Configuration = config;
    }
}
