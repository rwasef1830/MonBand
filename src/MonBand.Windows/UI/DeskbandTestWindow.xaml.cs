using System;
using MonBand.Core.Util;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    public partial class DeskbandTestWindow
    {
        readonly CrossProcessSignal _signal;

        public DeskbandTestWindow()
        {
            this.InitializeComponent();

            this._signal = new CrossProcessSignal(App.ReloadEventName);
            this.Reload();
            this._signal.Signaled += (_, __) => this.Reload();
        }

        void Reload()
        {
            var appSettings = AppSettings.Load();
            this.Dispatcher.Invoke(() => this.Control.AppSettings = appSettings);
        }

        protected override void OnClosed(EventArgs e)
        {
            this._signal.Dispose();
        }
    }
}
