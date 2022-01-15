using System;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Threading;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.Models.Settings;
using MonBand.Windows.Services;
using MonBand.Windows.UI;

namespace MonBand.Windows.ComHost
{
    [ComVisible(true)]
    [Guid("93A56AA2-22D3-4EA1-B11B-6025934FC260")]
    public class Deskband : CSDeskBandWpf
    {
        readonly AppSettingsService _appSettingsService;
        readonly LogLevelSignal _logLevelSignal;
        readonly ILoggerFactory _loggerFactory;
        readonly ILogger _log;
        readonly DeskbandControl _control;
        readonly CrossProcessSignal _processSignal;

        protected override UIElement UIElement { get; }

        public Deskband()
        {
            try
            {
                this._appSettingsService = new AppSettingsService();

                this._logLevelSignal = new LogLevelSignal();
                this._loggerFactory = LoggerConfiguration.CreateLoggerFactory(
                    LogLevel.Information,
                    this._appSettingsService.GetLogFilePath("Deskband"),
                    this._logLevelSignal);
                this._log = this._loggerFactory.CreateLogger(this.GetType().Name);

                var appSettings = this._appSettingsService.LoadOrCreate<SettingsModel>();
                this._logLevelSignal.Update(appSettings.LogLevel);

                this._control = new DeskbandControl(this._loggerFactory);
                this.UIElement = this._control;

                this.Options.MinHorizontalSize = new Size(150, 30);
                this.Options.HorizontalSize = new Size(150, 30);
                this.Options.MinVerticalSize = new Size(60, 150);
                this.Options.VerticalSize = new Size(60, 150);
                this.Options.Title = "MonBand";
                this.Options.ShowTitle = false;
                this.Options.IsFixed = false;
                this.Options.HeightIncrement = 1;
                this.Options.HeightCanChange = true;

                this._processSignal = new CrossProcessSignal(IAppSettingsService.ReloadEventName);
                this.Reload();
                this._control.Loaded += this.HandleControlLoaded;
            }
            catch (Exception ex)
            {
                this._log?.LogError(ex, "MonBand initialization failed");
                MessageBox.Show(
                    ex.ToString(),
                    "Failed to load MonBand Deskband",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
            this.UIElement.Dispatcher?.Invoke(() => this._control.Settings = appSettings);
        }

        protected override void DeskbandOnClosed()
        {
            this._processSignal.Dispose();
            this._loggerFactory.Dispose();
        }

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            RegistrationHelper.Register(t);
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            RegistrationHelper.Unregister(t);
        }
    }
}
