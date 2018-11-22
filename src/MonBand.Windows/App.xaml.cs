﻿using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.Settings;
using MonBand.Windows.UI;

namespace MonBand.Windows
{
    public partial class App
    {
        public static readonly ILoggerFactory LoggerFactory = LoggerConfiguration.CreateLoggerFactory(
            LogLevel.Information,
            "Settings");

        public const string ReloadEventName = nameof(MonBand) + "-Reload";

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            base.OnStartup(e);

            if (e.Args.FirstOrDefault() == "deskband-test")
            {
                this.MainWindow = new DeskbandTestWindow();
            }
            else
            {
                var settings = AppSettings.Load();
                this.MainWindow = new SettingsWindow(settings);
            }

            this.MainWindow.Show();
        }
    }
}
