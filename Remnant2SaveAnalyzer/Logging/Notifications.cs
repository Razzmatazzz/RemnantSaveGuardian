using System;
using System.Collections.Generic;

namespace Remnant2SaveAnalyzer.Logging;

internal static class Notifications
{
    public static event EventHandler<MessageLoggedEventArgs>? MessageLogged;
    public static List<LogMessage> Messages { get; } = [];

    public static void Log(string message, LogType logType)
    {
        var logger = Logging.Log.Logger
            .ForContext<RemnantSave>()
            .ForContext("RemnantLogCategory", "Notification");

        LogMessage m = new(message, logType);
        MessageLogged?.Invoke(null, new(m));
        Messages.Add(m);

        if (logType == LogType.Success || logType == LogType.Normal)
        {
            logger.Information(message);
        }

        if (logType == LogType.Warning)
        {
            logger.Warning(message);
        }

        if (logType == LogType.Error)
        {
            logger.Error(message);
        }
    }

    public static void Log(string message)
    {
        Log(message, LogType.Normal);
    }

    public static void Success(string message)
    {
        Log(message, LogType.Success);
    }

    public static void Error(string message)
    {
        Log(message, LogType.Error);
    }

    public static void Warn(string message)
    {
        Log(message, LogType.Warning);
    }
}

/*
public class RemnantNotificationsSink(ITextFormatter? formatter) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {

        string message;
        if (formatter == null)
        {
            message = logEvent.RenderMessage();
        }
        else
        {
            StringWriter tw = new();
            formatter.Format(logEvent, tw);
            message = tw.ToString();
        }

        if (logEvent.Level == LogEventLevel.Verbose ||
            logEvent.Level == LogEventLevel.Debug ||
            logEvent.Level == LogEventLevel.Information)
        {
            //Notifications.Log(message);
        }

        if (logEvent.Level == LogEventLevel.Warning)
        {
            //Notifications.Warn(message);
        }

        if (logEvent.Level == LogEventLevel.Error ||
            logEvent.Level == LogEventLevel.Fatal)
        {
            //Notifications.Error(message);
        }

    }
}

public static class RemnantNotificationsSinkExtensions
{
    public static LoggerConfiguration RemnantNotificationSink(
        this LoggerSinkConfiguration loggerConfiguration,
        ITextFormatter? formatter = null)
    {
        return loggerConfiguration.Sink(new RemnantNotificationsSink(formatter));
    }
}
*/