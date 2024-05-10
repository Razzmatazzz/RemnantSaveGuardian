using System;
using System.Collections.Generic;

namespace Remnant2SaveAnalyzer.Logging;

internal static class Notifications
{
    public static event EventHandler<MessageLoggedEventArgs>? MessageLogged;
    public static List<LogMessage> Messages { get; } = [];

    public static void Normal(string message)
    {
        Log.Logger
            .ForContext(typeof(Notifications))
            .ForContext("RemnantCategory", Log.Notification)
            .ForContext("RemnantNotificationType", "Normal")
            .Information(message);
    }

    public static void Success(string message)
    {
        Log.Logger
            .ForContext(typeof(Notifications))
            .ForContext("RemnantCategory", Log.Notification)
            .ForContext("RemnantNotificationType", "Success")
            .Information(message);
    }

    public static void Error(string message)
    {
        Log.Logger
            .ForContext(typeof(Notifications))
            .ForContext("RemnantCategory", Log.Notification)
            .ForContext("RemnantNotificationType", "Error")
            .Error(message);
    }

    public static void Warn(string message)
    {
        Log.Logger
            .ForContext(typeof(Notifications))
            .ForContext("RemnantCategory", Log.Notification)
            .ForContext("RemnantNotificationType", "Warning")
            .Warning(message);
    }

    public static void OnMessageLogged(MessageLoggedEventArgs e)
    {
        MessageLogged?.Invoke(null, e);
    }
}