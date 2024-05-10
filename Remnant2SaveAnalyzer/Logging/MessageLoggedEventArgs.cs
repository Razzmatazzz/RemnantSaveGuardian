using System;

namespace Remnant2SaveAnalyzer.Logging;

public class MessageLoggedEventArgs : EventArgs
{
    public LogMessage Message { get; set; }

    public MessageLoggedEventArgs(LogMessage message)
    {
        Message = message;
    }

    public MessageLoggedEventArgs(string message, LogType type)
    {
        Message = new(message, type);
    }

    public MessageLoggedEventArgs(string message) : this(message, LogType.Normal)
    {
    }
}
