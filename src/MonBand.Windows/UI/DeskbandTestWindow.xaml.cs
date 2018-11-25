using System;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    partial class DeskbandTestWindow
    {
        public static readonly ILoggerFactory LoggerFactory = LoggerConfiguration.CreateLoggerFactory(
            LogLevel.Information,
            "DeskbandTestWindow");

        readonly DeskbandControl _control;
        readonly CrossProcessSignal _signal;

        public DeskbandTestWindow()
        {
            this.InitializeComponent();

            this._control = new DeskbandControl(LoggerFactory);
            this.Content = this._control;
            this._signal = new CrossProcessSignal(App.ReloadEventName);

            this.Reload();
            this._signal.Signaled += (_, __) => this.Reload();
        }

        void Reload()
        {
            var appSettings = AppSettings.Load();
            this.Dispatcher.Invoke(() => this._control.AppSettings = appSettings);
        }

        protected override void OnClosed(EventArgs e)
        {
            this._signal.Dispose();
        }
    }
}
