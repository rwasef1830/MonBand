using System;
using System.Runtime.InteropServices;
using System.Windows;
using CSDeskBand;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.Settings;
using MonBand.Windows.UI;

namespace MonBand.Windows.ComHost
{
    [ComVisible(true)]
    [Guid("93A56AA2-22D3-4EA1-B11B-6025934FC260")]
    [CSDeskBandRegistration(Name = "MonBand")]
    public class Deskband : CSDeskBandWpf
    {
        public static readonly ILoggerFactory LoggerFactory = LoggerConfiguration.CreateLoggerFactory(
            LogLevel.Information,
            "Deskband");
        readonly ILogger _log;
        readonly DeskbandControl _control;
        readonly CrossProcessSignal _signal;

        protected override UIElement UIElement { get; }

        public Deskband()
        {
            this._log = LoggerFactory.CreateLogger(this.GetType().Name);

            try
            {
                this._control = new DeskbandControl(LoggerFactory);

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

                this._signal = new CrossProcessSignal(App.ReloadEventName);
                this.Reload();
                this._signal.Signaled += (_, __) => this.Reload();
            }
            catch (Exception ex)
            {
                this._log.LogError(ex, "MonBand initialization failed");
                MessageBox.Show(
                    ex.ToString(),
                    "Failed to load MonBand Deskband",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        void Reload()
        {
            var appSettings = AppSettings.Load();
            this.UIElement.Dispatcher?.Invoke(() => this._control.AppSettings = appSettings);
        }

        protected override void DeskbandOnClosed()
        {
            this._signal.Dispose();
        }
    }
}
