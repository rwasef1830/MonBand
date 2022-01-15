using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace MonBand.Windows.Bootstrap;

public static class LoggerConfiguration
{
    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    public static ILoggerFactory CreateLoggerFactory(LogLevel logLevel, string logFilePath,
        LogLevelSignal? signalOrNull)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(logFilePath));
        }

        var logLevelSwitch = new LoggingLevelSwitch { MinimumLevel = ConvertLevel(logLevel) };

        if (signalOrNull != null)
        {
            signalOrNull.LogLevelChanged += (_, level) => logLevelSwitch.MinimumLevel = ConvertLevel(level);
        }

        var configuration = new Serilog.LoggerConfiguration()
            .MinimumLevel.ControlledBy(logLevelSwitch)
            .Enrich.FromLogContext()
            .WriteTo
            .File(
                logFilePath,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}]{Scope} {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day);

        var logger = configuration.CreateLogger();
        return new LoggerFactory(new[] { new SerilogLoggerProvider(logger) });
    }

    static LogEventLevel ConvertLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Verbose
        };
    }
}
