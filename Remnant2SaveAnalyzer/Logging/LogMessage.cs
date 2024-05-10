namespace Remnant2SaveAnalyzer.Logging;

public class LogMessage(string text, NotificationType notificationType)
{
    public string Text { get; set; } = text;
    public NotificationType NotificationType { get; set; } = notificationType;
}
