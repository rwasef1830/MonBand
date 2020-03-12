using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Threading;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.Models.Settings;
using MonBand.Windows.Services;
using MonBand.Windows.UI;

namespace MonBand.Windows.Standalone.UI
{
    partial class DeskbandTestWindow
    {
        readonly LogLevelSignal _logLevelSignal;
        readonly IAppSettingsService _appSettingsService;
        readonly CrossProcessSignal _processSignal;
        readonly DeskbandControl _control;
        readonly ILogger _log;

        public DeskbandTestWindow(
            ILoggerFactory loggerFactory,
            LogLevelSignal logLevelSignal,
            IAppSettingsService appSettingsService,
            CrossProcessSignal processSignal)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            this._logLevelSignal = logLevelSignal ?? throw new ArgumentNullException(nameof(logLevelSignal));
            this._appSettingsService = appSettingsService
                                       ?? throw new ArgumentNullException(nameof(appSettingsService));
            this._processSignal = processSignal ?? throw new ArgumentNullException(nameof(processSignal));
            this._log = loggerFactory.CreateLogger(this.GetType().Name);

            this.InitializeComponent();

            this._control = new DeskbandControl(loggerFactory);
            this.Content = this._control;

            this.Reload();
            this._control.Loaded += this.HandleControlLoaded;
        }

        async void HandleControlLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                while (true)
                {
                    await this._processSignal
                        .WaitForSignalAsync()
                        .ConfigureAwait(true);
                    this.Reload();
                }
            }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                this._log.LogError(ex, "Unhandled exception in signal loop.");
            }
        }

        void Reload()
        {
            var appSettings = this._appSettingsService.LoadOrCreate<SettingsModel>();
            this._logLevelSignal.Update(appSettings.LogLevel);
            this.Dispatcher?.Invoke(() => this._control.Settings = appSettings);
        }
    }
}
