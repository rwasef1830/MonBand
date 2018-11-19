using System.Runtime.InteropServices;
using CSDeskBand;
using MonBand.Core.Util;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    [ComVisible(true)]
    [Guid("93A56AA2-22D3-4EA1-B11B-6025934FC260")]
    [CSDeskBandRegistration(Name = "MonBand")]
    public partial class Deskband
    {
        readonly CrossProcessSignal _signal;

        public Deskband()
        {
            this.InitializeComponent();

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

        void Reload()
        {
            var appSettings = AppSettings.Load();
            this.Dispatcher.Invoke(() => this.Control.AppSettings = appSettings);
        }

        protected override void OnClose()
        {
            this._signal.Dispose();
        }
    }
}
