using System;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using MonBand.Core.Snmp;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI.Commands
{
    class FetchInterfacesCommand : ICommand
    {
        readonly SettingsWindow _window;

        bool _executing;
        CancellationTokenSource _cancellationTokenSource;

        public event EventHandler CanExecuteChanged;

        public FetchInterfacesCommand(SettingsWindow window)
        {
            this._window = window ?? throw new ArgumentNullException(nameof(window));
            this._cancellationTokenSource = new CancellationTokenSource();

            window.Initialized += (_, __) =>
            {
                window.ListBoxMonitors.SelectionChanged += (sender, args) =>
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

        void ConfigOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object parameter)
        {
            var config = (SnmpPollerConfig)parameter;
            return !this._executing
                   && config != null
                   && !string.IsNullOrWhiteSpace(config.Address)
                   && config.Port > 0;
        }

        public async void Execute(object parameter)
        {
            var config = (SnmpPollerConfig)parameter;

            this._executing = true;
            this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);

            this._window.TextBoxAddress.IsReadOnly = true;
            this._window.TextBoxPort.IsReadOnly = true;
            this._window.TextBoxCommunity.IsReadOnly = true;
            this._window.ComboBoxInterfaceName.IsReadOnly = true;

            try
            {
                var cancellationTokenSource = Volatile.Read(ref this._cancellationTokenSource);

                var query = new SnmpInterfaceQuery(
                    new DnsEndPoint(config.Address, config.Port),
                    config.Community);

                var idsByName = await query
                    .GetIdsByNameAsync(cancellationTokenSource.Token)
                    .ConfigureAwait(true);

                this._window.ComboBoxInterfaceName.ItemsSource = idsByName.Keys;
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this._window,
                    $"Failed to fetch interfaces from {config.Address}:{config.Port}.\nError: {ex.Message}",
                    "Failed to fetch interfaces",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
            finally
            {
                this._window.TextBoxAddress.IsReadOnly = false;
                this._window.TextBoxPort.IsReadOnly = false;
                this._window.TextBoxCommunity.IsReadOnly = false;
                this._window.ComboBoxInterfaceName.IsReadOnly = false;

                this._executing = false;
                this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
