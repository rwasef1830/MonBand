using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.Services;
using MonBand.Windows.Standalone.UI;

namespace MonBand.Windows.Standalone
{
    public partial class App
    {
        readonly LogLevelSignal _logLevelSignal;
        ILoggerFactory _loggerFactory;
        ILogger _log;

        public App()
        {
            this._logLevelSignal = new LogLevelSignal();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            try
            {
                base.OnStartup(e);

                var appSettingsService = new AppSettingsService();
                var processSignal = new CrossProcessSignal(IAppSettingsService.ReloadEventName);

                if (e.Args.FirstOrDefault() == "deskband-test")
                {
                    this._loggerFactory = LoggerConfiguration.CreateLoggerFactory(
                        LogLevel.Information,
                        appSettingsService.GetLogFilePath("DeskbandTest"),
                        this._logLevelSignal);
                    this._log = this._loggerFactory.CreateLogger(this.GetType().Name);

                    this.MainWindow = new DeskbandTestWindow(
                        this._loggerFactory,
                        this._logLevelSignal,
                        appSettingsService,
                        processSignal);
                }
                else
                {
                    this._loggerFactory = LoggerConfiguration.CreateLoggerFactory(
                        LogLevel.Information,
                        appSettingsService.GetLogFilePath("Settings"),
                        this._logLevelSignal);
                    this._log = this._loggerFactory.CreateLogger(this.GetType().Name);

                    this.MainWindow = new SettingsWindow(this._loggerFactory, appSettingsService, processSignal);
                }

                this.MainWindow.Show();
            }
            catch (Exception ex)
            {
                this._log.LogError(ex, "MonBand initialization failed");
                MessageBox.Show(
                    ex.Message,
                    "MonBand initialization failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
