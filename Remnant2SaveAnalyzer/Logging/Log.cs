using System.IO;
using System.Linq;
using Remnant2SaveAnalyzer.Views.Windows;
using Serilog;
using Serilog.Templates;

namespace Remnant2SaveAnalyzer.Logging;

internal static class Log
{
    public static ILogger Logger => (_logger ?? Serilog.Log.Logger).ForContext("RemnantLogLibrary", "Remnant2SaveAnalyzer");

    static Log()
    {
        InitialiseSerilog();
        Properties.Settings.Default.PropertyChanged += Default_PropertyChanged;
    }

    private static void Default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "CreateLogFile" 
            || e.PropertyName != "ReportPerformance")
        {
            return;
        }
        InitialiseSerilog();
    }

    private static Serilog.Core.Logger? _logger;

    public static void InitialiseSerilog()
    {
        ExpressionTemplate et = new("{@t:dd MMM yyyy HH:mm:ss} {@l:u3} [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}] {@m}\n");

        LoggerConfiguration config = new LoggerConfiguration()
            .Filter.ByIncludingOnly(x =>
            {
                var debug = x.Properties["bla"];
                string category = x.Properties["RemnantLogCategory"].ToString();
                string[] knownCategories = [
                    "Parser", 
                    "Compression", 
                    "Performance", 
                    "UnknownItems",
                    "PlayerInfo",
                    "Notification",
                    "Prerequisites",
                ];
                return knownCategories.Contains(category);
            });
        //.WriteTo.RemnantNotificationSink();

        if (Properties.Settings.Default.CreateLogFile)
        {
            config = config.WriteTo.File(et, "log2.txt");
        }
        //if (Properties.Settings.Default.CreateLogFile) // Properties.Settings.Default.ReportAnalyserWarnings
        //{
        //    config = config.WriteTo.File("log2.txt");
        //}
        if (!Properties.Settings.Default.ReportPerformance)
        {
            config = config.Filter.ByExcluding(x => x.Properties["RemnantLogCategory"].ToString() == "Performance");
        }
        //if (!Properties.Settings.Default.CreateLogFile) // Properties.Settings.Default.ReportParserWarning
        //{
        //    config = config.Filter.ByExcluding(x => x.Properties["RemnantLogCategory"].ToString() == "Parser");
        //}
        //if (!Properties.Settings.Default.CreateLogFile) // Properties.Settings.Default.ReportCompressionWarning
        //{
        //    config = config.Filter.ByExcluding(x => x.Properties["RemnantLogCategory"].ToString() == "Compression");
        //}
        _logger?.Dispose();
        //File.WriteAllText("log.txt", DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": Version " + typeof(MainWindow).Assembly.GetName().Version + "\r\n");
        File.Delete("log2.txt");
        _logger = config.CreateLogger();
        lib.remnant2.analyzer.Log.Logger = _logger;
        lib.remnant2.saves.Log.Logger = _logger;
        _logger.Information($"Version {typeof(MainWindow).Assembly.GetName().Version}");
    }
}
