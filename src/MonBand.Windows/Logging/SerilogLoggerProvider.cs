using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using System.Threading;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;
using ILogger = Serilog.ILogger;

namespace MonBand.Windows.Logging
{
    // Copied from https://github.com/serilog/serilog-extensions-logging
    // because referencing Serilog.Extensions.Logging breaks assembly load
    // from COM due to inability to set binding redirects (it requires a redirect
    // for Microsoft.Extensions.Logging).
    sealed class SerilogLoggerProvider : ILoggerProvider, ILogEventEnricher
    {
        internal const string OriginalFormatPropertyName = "{OriginalFormat}";
        internal const string ScopePropertyName = "Scope";

        readonly ILogger _logger;

        public SerilogLoggerProvider(ILogger logger)
        {
            if (logger != null)
            {
                this._logger = logger.ForContext(new[] { this });
            }
        }

        public FrameworkLogger CreateLogger(string name)
        {
            return new SerilogLogger(this, this._logger, name);
        }

        public IDisposable BeginScope<T>(T state)
        {
            if (this.CurrentScope != null)
            {
                return new SerilogLoggerScope(this, state);
            }

            // The outermost scope pushes and pops the Serilog `LogContext` - once
            // this enricher is on the stack, the `CurrentScope` property takes care
            // of the rest of the `BeginScope()` stack.
            var popSerilogContext = LogContext.Push(this);
            return new SerilogLoggerScope(this, state, popSerilogContext);
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            List<LogEventPropertyValue> scopeItems = null;
            for (var scope = this.CurrentScope; scope != null; scope = scope.Parent)
            {
                scope.EnrichAndCreateScopeItem(logEvent, propertyFactory, out LogEventPropertyValue scopeItem);

                if (scopeItem != null)
                {
                    scopeItems = scopeItems ?? new List<LogEventPropertyValue>();
                    scopeItems.Add(scopeItem);
                }
            }

            if (scopeItems != null)
            {
                scopeItems.Reverse();
                logEvent.AddPropertyIfAbsent(new LogEventProperty(ScopePropertyName, new SequenceValue(scopeItems)));
            }
        }

        readonly AsyncLocal<SerilogLoggerScope> _value = new AsyncLocal<SerilogLoggerScope>();

        internal SerilogLoggerScope CurrentScope
        {
            get => this._value.Value;
            set => this._value.Value = value;
        }

        public void Dispose() { }
    }
}
