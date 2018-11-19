using System.Runtime.InteropServices;
using CSDeskBand;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    [ComVisible(true)]
    [Guid("93A56AA2-22D3-4EA1-B11B-6025934FC260")]
    [CSDeskBandRegistration(Name = "MonBand")]
    public partial class Deskband
    {
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

            this.Control.AppSettings = AppSettings.Load();
        }
    }
}
