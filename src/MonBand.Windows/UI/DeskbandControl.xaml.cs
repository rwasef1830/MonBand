using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Core.Util;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    public partial class DeskbandControl
    {
        readonly IDictionary<ITrafficRateService, CompactMonitorView> _viewsByService;

        public DeskbandControl() : this(AppSettings.Load()) { }

        public DeskbandControl(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            this._viewsByService = new Dictionary<ITrafficRateService, CompactMonitorView>();

            this.InitializeComponent();
            this.InitializeMonitors(settings.SnmpPollers);
        }

        void InitializeMonitors(IList<SnmpPollerConfig> snmpPollers)
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
                    new NullLoggerFactory());

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
