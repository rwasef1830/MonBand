using System;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    public partial class DeskbandTestWindow
    {
        public DeskbandTestWindow(AppSettings settings)
        {
            this.InitializeComponent();
            this.Control.AppSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
    }
}
