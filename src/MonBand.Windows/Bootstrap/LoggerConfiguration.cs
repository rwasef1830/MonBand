using System;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace MonBand.Windows.Bootstrap
{
    public static class LoggerConfiguration
    {
        public static ILoggerFactory CreateLoggerFactory(LogLevel logLevel, string logFilePath, LogLevelSignal signalOrNull)
        {
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(logFilePath));
            }

            var logLevelSwitch = new LoggingLevelSwitch { MinimumLevel = ConvertLevel(logLevel) };

            if (signalOrNull != null)
            {
                signalOrNull.LoggingLevelChanged += (_, level) => logLevelSwitch.MinimumLevel = ConvertLevel(level);
            }

            var configuration = new Serilog.LoggerConfiguration()
                .MinimumLevel.ControlledBy(logLevelSwitch)
                .Enrich.FromLogContext()
                .WriteTo
                .File(
                    logFilePath,
                    outputTemplate:
                    "[{Timestamp:u} {Level:u3}] [{SourceContext}]{Scope} {Message}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day);

            var logger = configuration.CreateLogger();
            return new LoggerFactory(new[] { new SerilogLoggerProvider(logger) });
        }

        static LogEventLevel ConvertLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Debug:
                    return LogEventLevel.Debug;
                case LogLevel.Information:
                    return LogEventLevel.Information;
                case LogLevel.Warning:
                    return LogEventLevel.Warning;
                case LogLevel.Error:
                    return LogEventLevel.Error;
                case LogLevel.Critical:
                    return LogEventLevel.Fatal;
                default:
                    return LogEventLevel.Verbose;
            }
        }
    }
}
