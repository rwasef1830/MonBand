using System;
using System.Linq;
using System.Windows;
using MonBand.Windows.Settings;
using MonBand.Windows.UI;

namespace MonBand.Windows
{
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            base.OnStartup(e);

            var settings = AppSettings.Load();

            if (e.Args.FirstOrDefault() == "deskband-test")
            {
                this.MainWindow = new DeskbandTestWindow(settings);
            }
            else
            {
                this.MainWindow = new SettingsWindow(settings);
            }

            this.MainWindow.Show();
        }
    }
}
