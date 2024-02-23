using Microsoft.Extensions.Logging;
using ZLogger;

namespace KESCompiler.Compiler;

public interface ILogger
{
    public int ErrorCount { get; }
    public int WarningCount { get; }
    public void Reset();
    public void Log(string message, object[]? optional = null);
    public void Warning(CodePosition pos, KesErrorType errorType, object[]? optional = null);
    public void Error(CodePosition pos, KesErrorType errorType, object[]? optional = null);
}

public static class ConsoleFactory
{
    public static ILogger Create(Microsoft.Extensions.Logging.ILogger logger)
    {
        return new StandardLogger();
    }
}

class StandardLogger : ILogger
{
    readonly Microsoft.Extensions.Logging.ILogger _logger;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }

    public StandardLogger()
    {
        using var factory = LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddZLoggerConsole();
            logging.AddZLoggerFile("logs/kes.log");
        });
        _logger = factory.CreateLogger<StandardLogger>();
    }

    public void Reset()
    {
        ErrorCount = 0;
        WarningCount = 0;
    }
    public void Log(string message, object[]? optional = null)
    {
        var str = optional is null ? message : string.Format(message, optional);
        _logger.ZLogInformation($"{str}");
    }
    public void Warning(CodePosition pos, KesErrorType errorType, object[]? optional = null)
    {
        var message = optional is null ? errorType.message : string.Format(errorType.message, optional);
        _logger.ZLogWarning($"{pos.filename} [{pos.line}:{pos.count}] KES{errorType.id:D4} : {message}");
        WarningCount++;
    }
    public void Error(CodePosition pos, KesErrorType errorType, object[]? optional = null)
    {
        var message = optional is null ? errorType.message : string.Format(errorType.message, optional);
        _logger.ZLogError($"{pos.filename} [{pos.line}:{pos.count}] KES{errorType.id:D4} : {message}");
        ErrorCount++;
    }
}