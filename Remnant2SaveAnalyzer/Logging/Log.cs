using System;
using System.ComponentModel;
using System.IO;
using Remnant2SaveAnalyzer.Views.Windows;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;

namespace Remnant2SaveAnalyzer.Logging;

internal static class Log
{
    //    string[] knownCategories = [
    //        "Parser", 
    //        "Compression", 
    //        "Performance", 
    //        "UnknownItems",
    //        PlayerInfo,
    //        Notification,
    //        "Prerequisites",
    //    ];

    public const string Category = "RemnantLogCategory";
    public const string PlayerInfo = "PlayerInfo";
    public const string Notification = "Notification";

    private const string LogFileName = "log.txt";
    public static ILogger Logger => (_logger ?? Serilog.Log.Logger).ForContext("RemnantLogLibrary", "Remnant2SaveAnalyzer");
    private static bool _startUp = true;
    private static readonly LoggingLevelSwitch Switch = new();

    public static void StartUpFinished()
    {
        _startUp = false;
    }

    static Log()
    {
        Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
        InitialiseSerilog();
    }

    private static void Default_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "LogLevel")
        {
            UpdateLogLevel();
        }
    }

    private static void UpdateLogLevel()
    {
        string level = Properties.Settings.Default.LogLevel;
        if (Enum.TryParse(level, true, out LogEventLevel l))
        {
            Switch.MinimumLevel = l;
        }
    }

    private static Logger? _logger;

    public static void InitialiseSerilog()
    {
        lock (LogFileName)
        {
            _logger?.Dispose();
            if (Properties.Settings.Default.CreateLogFile)
            {
                File.Delete(LogFileName);
            }

            ExpressionTemplate et = new("{@t:dd MMM yyyy HH:mm:ss} {@l:u3} [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}] {@m}\n");

            UpdateLogLevel();

            string compression = lib.remnant2.saves.Log.Compression;
            string parser = lib.remnant2.saves.Log.Parser;
            string performance = lib.remnant2.analyzer.Log.Performance;
            string prerequisites = lib.remnant2.analyzer.Log.Prerequisites;
            string unknownItems = lib.remnant2.analyzer.Log.UnknownItems;

            LoggerConfiguration config = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(Switch)
                .WriteTo.Conditional(
                    _ => Properties.Settings.Default.CreateLogFile, lc => lc.File(et, LogFileName))
                .Filter.ByExcluding(x =>
                    (!Properties.Settings.Default.ReportPerformance || !_startUp)
                    && x.Properties.ContainsKey(Category)
                    && x.Properties[Category].ToString().Trim('"') == performance)
                .Filter.ByExcluding(x =>
                    (!Properties.Settings.Default.ReportParserWarnings || !_startUp)
                    && x.Properties.ContainsKey(Category)
                    && (x.Properties[Category].ToString().Trim('"') == parser
                        || x.Properties[Category].ToString().Trim('"') == compression))
                .Filter.ByExcluding(x =>
                    (!Properties.Settings.Default.DebugPrerequisites || !_startUp)
                    && x.Properties.ContainsKey(Category)
                    && x.Properties[Category].ToString().Trim('"') == prerequisites)
                .Filter.ByExcluding(x =>
                    x.Properties.ContainsKey(Category)
                    && x.Properties[Category].ToString().Trim('"') == unknownItems
                    && !_startUp)
                .WriteTo.Conditional(x => x.Properties.ContainsKey("RemnantNotificationType"),
                    lc => lc.RemnantNotificationSink());

            _logger = config.CreateLogger();
            lib.remnant2.analyzer.Log.Logger = _logger;
            lib.remnant2.saves.Log.Logger = _logger;
            _logger.ForContext(typeof(Log)).Information($"Version {typeof(MainWindow).Assembly.GetName().Version}");
        }
    }
}
