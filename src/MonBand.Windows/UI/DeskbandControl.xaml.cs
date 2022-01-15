using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MonBand.Core;
using MonBand.Core.PerformanceCounters;
using MonBand.Core.Snmp;
using MonBand.Windows.Models.Settings;

namespace MonBand.Windows.UI;

public partial class DeskbandControl
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings),
        typeof(SettingsModel),
        typeof(DeskbandControl),
        new PropertyMetadata { PropertyChangedCallback = AppSettingsChanged });

    readonly IDictionary<ITrafficRateService, CompactMonitorView> _viewsByService;
    readonly ILoggerFactory _loggerFactory;

    public SettingsModel Settings
    {
        get => (SettingsModel)this.GetValue(SettingsProperty);
        set => this.SetValue(SettingsProperty, value);
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

        foreach (var (trafficRateService, _) in self._viewsByService)
        {
            trafficRateService.Dispose();
            trafficRateService.TrafficRateUpdated -= self.HandleTrafficRateUpdated;
        }

        self._viewsByService.Clear();

        var newAppSettings = (SettingsModel)e.NewValue;

        self.RootGrid.Children.Clear();
        self.RootGrid.ColumnDefinitions.Clear();

        self.InitializeSnmpPollers(new ReadOnlyCollection<SnmpPollerConfig>(newAppSettings.SnmpPollers));
        self.InitializePerformanceCounterPollers(
            new ReadOnlyCollection<PerformanceCounterPollerConfig>(newAppSettings.PerformanceCounterPollers));
    }

    void InitializeSnmpPollers(IReadOnlyList<SnmpPollerConfig> snmpPollers)
    {
        if (snmpPollers == null) throw new ArgumentNullException(nameof(snmpPollers));

        foreach (var snmpPoller in snmpPollers)
        {
            var trafficRateService = new SnmpTrafficRateService(
                new SnmpNamedInterfaceTrafficQuery(
                    new DnsEndPoint(snmpPoller.Address, snmpPoller.Port),
                    snmpPoller.Community,
                    snmpPoller.InterfaceName),
                snmpPoller.PollIntervalSeconds,
                this._loggerFactory);

            trafficRateService.TrafficRateUpdated += this.HandleTrafficRateUpdated;

            var view = new CompactMonitorView { MonitorName = "SNMP: " + snmpPoller };
            this._viewsByService[trafficRateService] = view;

            this.RootGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            Grid.SetColumn(view, this.RootGrid.Children.Count);

            this.RootGrid.Children.Add(view);
            trafficRateService.Start();
        }
    }

    void InitializePerformanceCounterPollers(IReadOnlyList<PerformanceCounterPollerConfig> performanceCounterPollers)
    {
        if (performanceCounterPollers == null) throw new ArgumentNullException(nameof(performanceCounterPollers));

        foreach (var performanceCounterPoller in performanceCounterPollers)
        {
            var trafficRateService = new PerformanceCounterTrafficRateService(
                performanceCounterPoller.InterfaceName,
                this._loggerFactory);

            trafficRateService.TrafficRateUpdated += this.HandleTrafficRateUpdated;

            var view = new CompactMonitorView { MonitorName = "Performance counters: " + performanceCounterPoller };
            this._viewsByService[trafficRateService] = view;

            this.RootGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            Grid.SetColumn(view, this.RootGrid.Children.Count);

            this.RootGrid.Children.Add(view);
            trafficRateService.Start();
        }
    }

    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    void HandleTrafficRateUpdated(object? sender, NetworkTraffic traffic)
    {
        if (sender == null)
        {
            return;
        }
        
        var view = this._viewsByService[(ITrafficRateService)sender];
        this.Dispatcher?.Invoke(() => view.AddTraffic(traffic));
    }
}
