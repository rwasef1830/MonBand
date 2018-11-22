using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;
using ILogger = Serilog.ILogger;

namespace MonBand.Windows.Logging
{
    class SerilogLogger : FrameworkLogger
    {
        readonly SerilogLoggerProvider _provider;
        readonly ILogger _logger;

        static readonly MessageTemplateParser s_MessageTemplateParser = new MessageTemplateParser();

        public SerilogLogger(
            SerilogLoggerProvider provider,
            ILogger logger = null,
            string name = null)
        {
            this._provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this._logger = logger;

            // If a logger was passed, the provider has already added itself as an enricher
            this._logger = this._logger ?? Serilog.Log.Logger.ForContext(new[] { provider });

            if (name != null)
            {
                this._logger = this._logger.ForContext(Constants.SourceContextPropertyName, name);
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return this._logger.IsEnabled(ConvertLevel(logLevel));
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this._provider.BeginScope(state);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var level = ConvertLevel(logLevel);
            if (!this._logger.IsEnabled(level))
            {
                return;
            }

            var logger = this._logger;
            string messageTemplate = null;

            var properties = new List<LogEventProperty>();

            if (state is IEnumerable<KeyValuePair<string, object>> structure)
            {
                foreach (var property in structure)
                {
                    if (property.Key == SerilogLoggerProvider.OriginalFormatPropertyName && property.Value is string s)
                    {
                        messageTemplate = s;
                    }
                    else if (property.Key.StartsWith("@"))
                    {
                        if (logger.BindProperty(property.Key.Substring(1), property.Value, true, out var destructured))
                        {
                            properties.Add(destructured);
                        }
                    }
                    else
                    {
                        if (logger.BindProperty(property.Key, property.Value, false, out var bound))
                        {
                            properties.Add(bound);
                        }
                    }
                }

                var stateType = state.GetType();
                var stateTypeInfo = stateType.GetTypeInfo();
                // Imperfect, but at least eliminates `1 names
                if (messageTemplate == null && !stateTypeInfo.IsGenericType)
                {
                    messageTemplate = "{" + stateType.Name + ":l}";
                    if (logger.BindProperty(
                        stateType.Name,
                        AsLoggableValue(state, formatter),
                        false,
                        out var stateTypeProperty))
                    {
                        properties.Add(stateTypeProperty);
                    }
                }
            }

            if (messageTemplate == null)
            {
                string propertyName = null;
                if (state != null)
                {
                    propertyName = "State";
                    messageTemplate = "{State:l}";
                }
                else if (formatter != null)
                {
                    propertyName = "Message";
                    messageTemplate = "{Message:l}";
                }

                if (propertyName != null)
                {
                    if (logger.BindProperty(propertyName, AsLoggableValue(state, formatter), false, out var property))
                    {
                        properties.Add(property);
                    }
                }
            }

            if (eventId.Id != 0 || eventId.Name != null)
            {
                properties.Add(CreateEventIdProperty(eventId));
            }

            var parsedTemplate = s_MessageTemplateParser.Parse(messageTemplate ?? "");
            var evt = new LogEvent(DateTimeOffset.Now, level, exception, parsedTemplate, properties);
            logger.Write(evt);
        }

        static object AsLoggableValue<TState>(TState state, Func<TState, Exception, string> formatter)
        {
            object sObj = state;
            if (formatter != null)
            {
                sObj = formatter(state, null);
            }

            return sObj;
        }

        static LogEventLevel ConvertLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return LogEventLevel.Fatal;
                case LogLevel.Error:
                    return LogEventLevel.Error;
                case LogLevel.Warning:
                    return LogEventLevel.Warning;
                case LogLevel.Information:
                    return LogEventLevel.Information;
                case LogLevel.Debug:
                    return LogEventLevel.Debug;
                // ReSharper disable once RedundantCaseLabel
                case LogLevel.Trace:
                default:
                    return LogEventLevel.Verbose;
            }
        }

        static LogEventProperty CreateEventIdProperty(EventId eventId)
        {
            var properties = new List<LogEventProperty>(2);

            if (eventId.Id != 0)
            {
                properties.Add(new LogEventProperty("Id", new ScalarValue(eventId.Id)));
            }

            if (eventId.Name != null)
            {
                properties.Add(new LogEventProperty("Name", new ScalarValue(eventId.Name)));
            }

            return new LogEventProperty("EventId", new StructureValue(properties));
        }
    }
}
