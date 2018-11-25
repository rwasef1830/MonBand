using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Core.Util;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    public partial class DeskbandControl
    {
        public static readonly DependencyProperty AppSettingsProperty = DependencyProperty.Register(
            nameof(AppSettings),
            typeof(AppSettings),
            typeof(DeskbandControl),
            new PropertyMetadata { PropertyChangedCallback = AppSettingsChanged });

        readonly IDictionary<ITrafficRateService, CompactMonitorView> _viewsByService;
        readonly ILoggerFactory _loggerFactory;

        public AppSettings AppSettings
        {
            get => (AppSettings)this.GetValue(AppSettingsProperty);
            set => this.SetValue(AppSettingsProperty, value);
        }

        public DeskbandControl(ILoggerFactory loggerFactory)
        {
            this._viewsByService = new Dictionary<ITrafficRateService, CompactMonitorView>();
            this._loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.InitializeComponent();
        }

        static void AppSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (DeskbandControl)d;

            foreach (var item in self._viewsByService)
            {
                item.Key.Dispose();
                item.Key.TrafficRateUpdated -= self.HandleTrafficRateUpdated;
            }

            self._viewsByService.Clear();

            var newAppSettings = (AppSettings)e.NewValue;
            self.InitializeSnmpPollers(new ReadOnlyCollection<SnmpPollerConfig>(newAppSettings.SnmpPollers));
        }

        void InitializeSnmpPollers(IReadOnlyList<SnmpPollerConfig> snmpPollers)
        {
            if (snmpPollers == null) throw new ArgumentNullException(nameof(snmpPollers));

            for (var i = 0; i < snmpPollers.Count; i++)
            {
                var snmpPoller = snmpPollers[i];

                var trafficRateService = new SnmpPollingTrafficRateService(
                    new SnmpNamedInterfaceTrafficQuery(
                        new DnsEndPoint(snmpPoller.Address, snmpPoller.Port),
                        snmpPoller.Community,
                        snmpPoller.InterfaceName),
                    3,
                    SystemTimeProvider.Instance,
                    this._loggerFactory);

                trafficRateService.TrafficRateUpdated += this.HandleTrafficRateUpdated;

                var view = new CompactMonitorView { MonitorName = snmpPoller.ToString() };
                this._viewsByService[trafficRateService] = view;

                Grid.SetColumn(view, i);
                this.RootGrid.Children.Add(view);

                trafficRateService.Start();
            }
        }

        void HandleTrafficRateUpdated(object sender, NetworkTraffic traffic)
        {
            var view = this._viewsByService[(ITrafficRateService)sender];
            this.Dispatcher.Invoke(() => view.AddTraffic(traffic));
        }
    }
}
