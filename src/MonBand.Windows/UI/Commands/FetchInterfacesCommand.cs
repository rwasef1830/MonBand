using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
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

        public event EventHandler CanExecuteChanged;

        public FetchInterfacesCommand(SettingsWindow window)
        {
            this._window = window ?? throw new ArgumentNullException(nameof(window));

            window.Initialized += (_, __) =>
            {
                window.ListBoxMonitors.SelectionChanged += (sender, args) =>
                {
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
            this._window.ComboBoxInterfaceName.IsReadOnly = true;

            try
            {
                var ipAddresses = await Dns.GetHostAddressesAsync(config.Address).ConfigureAwait(true);
                var query = new SnmpInterfaceQuery(
                    new IPEndPoint(ipAddresses.First(), config.Port),
                    config.Community);
                var idsByName = await query.GetIdsByNameAsync().ConfigureAwait(true);
                this._window.ComboBoxInterfaceName.ItemsSource = idsByName.Keys;
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
                this._window.ComboBoxInterfaceName.IsReadOnly = false;
                this._executing = false;
                this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
