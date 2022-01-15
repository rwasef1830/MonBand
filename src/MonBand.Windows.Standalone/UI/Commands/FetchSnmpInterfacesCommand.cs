using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using MonBand.Core.Snmp;
using MonBand.Windows.Models.Settings;
using MonBand.Windows.Standalone.UI.Settings;

namespace MonBand.Windows.Standalone.UI.Commands;

[SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
class FetchSnmpInterfacesCommand : ICommand
{
    readonly SnmpMonitorsControl _snmpMonitors;

    bool _executing;
    CancellationTokenSource _cancellationTokenSource;

    public event EventHandler? CanExecuteChanged;

    public FetchSnmpInterfacesCommand(SnmpMonitorsControl tab)
    {
        this._snmpMonitors = tab ?? throw new ArgumentNullException(nameof(tab));
        this._cancellationTokenSource = new CancellationTokenSource();

        tab.Initialized += (_, _) =>
        {
            tab.ListBoxMonitors.SelectionChanged += (_, args) =>
            {
                var oldCancellationTokenSource = Interlocked.Exchange(
                    ref this._cancellationTokenSource,
                    new CancellationTokenSource());
                oldCancellationTokenSource.Cancel();

                this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);

                foreach (SnmpPollerConfig config in args.RemovedItems)
                {
                    config.PropertyChanged -= this.ConfigOnPropertyChanged;
                }

                foreach (SnmpPollerConfig config in args.AddedItems)
                {
                    config.PropertyChanged += this.ConfigOnPropertyChanged;
                }
            };
        };
    }

    void ConfigOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanExecute(object? parameter)
    {
        var config = (SnmpPollerConfig?)parameter;
        return !this._executing
               && config != null
               && !string.IsNullOrWhiteSpace(config.Address)
               && config.Port > 0;
    }

    public async void Execute(object? parameter)
    {
        var config = (SnmpPollerConfig?)parameter;
        if (config == null)
        {
            throw new InvalidOperationException("Parameter must be of type SnmpPollerConfig");
        }

        this._executing = true;
        this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        this._snmpMonitors.TextBoxAddress.IsReadOnly = true;
        this._snmpMonitors.TextBoxPort.IsReadOnly = true;
        this._snmpMonitors.TextBoxCommunity.IsReadOnly = true;
        this._snmpMonitors.ComboBoxInterfaceName.IsReadOnly = true;

        try
        {
            var cancellationTokenSource = Volatile.Read(ref this._cancellationTokenSource);

            var query = new SnmpInterfaceQuery(
                new DnsEndPoint(config.Address, config.Port),
                config.Community);

            var idsByName = await query
                .GetIdsByNameAsync(cancellationTokenSource.Token)
                .ConfigureAwait(true);

            this._snmpMonitors.ComboBoxInterfaceName.ItemsSource = idsByName.Keys;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            var window = Window.GetWindow(this._snmpMonitors);

            if (window != null)
            {
                MessageBox.Show(
                    window,
                    $"Failed to fetch interfaces from {config.Address}:{config.Port}.\nError: {ex.Message}",
                    "Failed to fetch interfaces",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
            else
            {
                MessageBox.Show(
                    $"Failed to fetch interfaces from {config.Address}:{config.Port}.\nError: {ex.Message}",
                    "Failed to fetch interfaces",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
        }
        finally
        {
            this._snmpMonitors.TextBoxAddress.IsReadOnly = false;
            this._snmpMonitors.TextBoxPort.IsReadOnly = false;
            this._snmpMonitors.TextBoxCommunity.IsReadOnly = false;
            this._snmpMonitors.ComboBoxInterfaceName.IsReadOnly = false;

            this._executing = false;
            this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
