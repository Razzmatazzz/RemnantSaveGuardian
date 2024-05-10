using Serilog;
using Serilog.Configuration;
using Serilog.Formatting;

namespace Remnant2SaveAnalyzer.Logging;

public static class RemnantNotificationsSinkExtensions
{
    public static LoggerConfiguration RemnantNotificationSink(
        this LoggerSinkConfiguration loggerConfiguration,
        ITextFormatter? formatter = null)
    {
        return loggerConfiguration.Sink(new RemnantNotificationsSink(formatter));
    }
}
