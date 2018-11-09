using System;
using System.Windows.Controls;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    public partial class DeskbandTestWindow
    {
        public DeskbandTestWindow(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            this.InitializeComponent();

            var deskbandControl = new DeskbandControl(settings);
            Grid.SetRow(deskbandControl, 0);
            Grid.SetColumn(deskbandControl, 0);

            ((Grid)this.Content).Children.Add(deskbandControl);
        }
    }
}
