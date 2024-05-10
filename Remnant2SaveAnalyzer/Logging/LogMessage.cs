namespace Remnant2SaveAnalyzer.Logging;

public class LogMessage(string text, LogType logType)
{
    public string Text { get; set; } = text;
    public LogType LogType { get; set; } = logType;
}
