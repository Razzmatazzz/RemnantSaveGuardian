using System;
using System.IO;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Remnant2SaveAnalyzer.Logging;

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

        if (!logEvent.Properties.ContainsKey("RemnantNotificationType"))
        {
            return;
        }

        if (logEvent.Properties["RemnantNotificationType"] is not ScalarValue scalar)
        {
            return;
        }

        if (scalar.Value is not string value)
        {
            return;
        }

        if (!Enum.TryParse(typeof(NotificationType), value, out object? type))
        {
            return;
        }

        LogMessage m = new(message, (NotificationType)type);
        Notifications.OnMessageLogged(new(m));
        Notifications.Messages.Add(m);
    }
}
