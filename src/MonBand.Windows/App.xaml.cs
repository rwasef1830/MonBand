using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.UI;

namespace MonBand.Windows
{
    public partial class App
    {
        public static readonly ILoggerFactory LoggerFactory = LoggerConfiguration.CreateLoggerFactory(
            LogLevel.Information,
            "Settings");

        public const string ReloadEventName = nameof(MonBand) + "-Reload";

        readonly ILogger _log;

        public App()
        {
            this._log = LoggerFactory.CreateLogger(this.GetType().Name);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            try
            {
                base.OnStartup(e);

                if (e.Args.FirstOrDefault() == "deskband-test")
                {
                    this.MainWindow = new DeskbandTestWindow();
                }
                else
                {
                    this.MainWindow = new SettingsWindow();
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
