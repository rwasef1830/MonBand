using System;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace MonBand.Windows.Logging
{
    sealed class SerilogLoggerScope : IDisposable
    {
        const string c_NoName = "None";

        readonly SerilogLoggerProvider _provider;
        readonly object _state;
        readonly IDisposable _chainedDisposable;

        // An optimization only, no problem if there are data races on this.
        bool _disposed;

        public SerilogLoggerScope(SerilogLoggerProvider provider, object state, IDisposable chainedDisposable = null)
        {
            this._provider = provider;
            this._state = state;

            this.Parent = this._provider.CurrentScope;
            this._provider.CurrentScope = this;
            this._chainedDisposable = chainedDisposable;
        }

        public SerilogLoggerScope Parent { get; }

        public void Dispose()
        {
            if (!this._disposed)
            {
                this._disposed = true;

                // In case one of the parent scopes has been disposed out-of-order, don't
                // just blindly reinstate our own parent.
                for (var scan = this._provider.CurrentScope; scan != null; scan = scan.Parent)
                {
                    if (ReferenceEquals(scan, this))
                        this._provider.CurrentScope = this.Parent;
                }

                this._chainedDisposable?.Dispose();
            }
        }

        public void EnrichAndCreateScopeItem(LogEvent logEvent, ILogEventPropertyFactory propertyFactory, out LogEventPropertyValue scopeItem)
        {
            if (this._state == null)
            {
                scopeItem = null;
                return;
            }

            if (this._state is IEnumerable<KeyValuePair<string, object>> stateProperties)
            {
                scopeItem = null; // Unless it's `FormattedLogValues`, these are treated as property bags rather than scope items.

                foreach (var stateProperty in stateProperties)
                {
                    if (stateProperty.Key == SerilogLoggerProvider.OriginalFormatPropertyName && stateProperty.Value is string)
                    {
                        scopeItem = new ScalarValue(this._state.ToString());
                        continue;
                    }

                    var key = stateProperty.Key;
                    var destructureObject = false;

                    if (key.StartsWith("@"))
                    {
                        key = key.Substring(1);
                        destructureObject = true;
                    }

                    var property = propertyFactory.CreateProperty(key, stateProperty.Value, destructureObject);
                    logEvent.AddPropertyIfAbsent(property);
                }
            }
            else
            {
                scopeItem = propertyFactory.CreateProperty(c_NoName, this._state).Value;
            }
        }
    }
}
