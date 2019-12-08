using System;
using Microsoft.Extensions.Logging;
using MonBand.Windows.Settings;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace MonBand.Windows.Bootstrap
{
    public static class LoggerConfiguration
    {
        public static ILoggerFactory CreateLoggerFactory(LogLevel logLevel, string logFileNameSuffix)
        {
            if (string.IsNullOrWhiteSpace(logFileNameSuffix))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(logFileNameSuffix));
            }

            var configuration = new Serilog.LoggerConfiguration()
                .MinimumLevel.Is(ConvertLevel(logLevel))
                .Enrich.FromLogContext()
                .WriteTo
                .File(
                    AppSettings.GetLogFilePath(logFileNameSuffix),
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
