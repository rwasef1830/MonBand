﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Windows.Infrastructure.Input;
using MonBand.Windows.Models;
using MonBand.Windows.Models.Settings;
using MonBand.Windows.Standalone.UI.Commands;

namespace MonBand.Windows.Standalone.UI.Settings;

[SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
partial class SnmpMonitorsControl
{
    public static readonly DependencyProperty LoggerFactoryProperty = DependencyProperty.Register(
        nameof(LoggerFactory),
        typeof(ILoggerFactory),
        typeof(SnmpMonitorsControl));

    public static readonly DependencyProperty PollersProperty = DependencyProperty.Register(
        nameof(Pollers),
        typeof(ObservableCollection<SnmpPollerConfig>),
        typeof(SnmpMonitorsControl));

    ITrafficRateService? _trafficRateService;

    public ILoggerFactory LoggerFactory
    {
        get => (ILoggerFactory?)this.GetValue(LoggerFactoryProperty) ?? NullLoggerFactory.Instance;
        set => this.SetValue(LoggerFactoryProperty, value);
    }

    public ObservableCollection<SnmpPollerConfig> Pollers
    {
        get => (ObservableCollection<SnmpPollerConfig>)this.GetValue(PollersProperty);
        set => this.SetValue(PollersProperty, value);
    }

    public ICommand AddMonitorCommand { get; }
    public ICommand FetchInterfacesCommand { get; }
    public ICommand DeleteMonitorCommand { get; }
    public BandwidthPlotModel PlotModel { get; }

    public SnmpMonitorsControl()
    {
        this.AddMonitorCommand = new DelegateCommand(
            _ => this.Pollers.Add(
                new SnmpPollerConfig
                {
                    Address = "127.0.0.1",
                    Port = 161,
                    Community = "public"
                }));
        this.FetchInterfacesCommand = new FetchSnmpInterfacesCommand(this);
        this.DeleteMonitorCommand = new DelegateCommand(o =>
        {
            if (o != null)
            {
                this.Pollers.Remove((SnmpPollerConfig)o);
            }
        });

        this.PlotModel = new BandwidthPlotModel(100);
        this.InitializeComponent();
    }

    void HandleListBoxMonitorsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        this.GridMonitorForm.Visibility = e.AddedItems.Count == 0 ? Visibility.Hidden : Visibility.Visible;

        foreach (SnmpPollerConfig config in e.RemovedItems)
        {
            config.PropertyChanged -= this.HandleConfigOnPropertyChanged;
        }

        foreach (SnmpPollerConfig config in e.AddedItems)
        {
            config.PropertyChanged += this.HandleConfigOnPropertyChanged;
            this.UpdateTrafficRateService(config);
        }
    }

    void HandleConfigOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == null)
        {
            return;
        }
        
        this.UpdateTrafficRateService((SnmpPollerConfig)sender);
    }

    void UpdateTrafficRateService(SnmpPollerConfig config)
    {
        try
        {
            var remoteEndPoint = new DnsEndPoint(config.Address, config.Port);
            var newTrafficRateService = new SnmpTrafficRateService(
                new SnmpNamedInterfaceTrafficQuery(remoteEndPoint, config.Community, config.InterfaceName),
                config.PollIntervalSeconds,
                this.LoggerFactory);

            var oldTrafficRateService = Interlocked.Exchange(
                ref this._trafficRateService,
                newTrafficRateService);
            if (oldTrafficRateService != null)
            {
                oldTrafficRateService.TrafficRateUpdated -= this.HandleTrafficRateUpdated;
                oldTrafficRateService.Dispose();
            }

            this.PlotModel.Reset();
            this.Dispatcher?.Invoke(() => this.PlotModel.InvalidatePlot(true));
            newTrafficRateService.TrafficRateUpdated += this.HandleTrafficRateUpdated;
            newTrafficRateService.Start();
        }
        catch (SocketException)
        {
            // ignore
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            var window = Window.GetWindow(this);

            if (window != null)
            {
                MessageBox.Show(
                    window,
                    ex.Message,
                    "Error updating bandwidth chart",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
            else
            {
                MessageBox.Show(
                    ex.Message,
                    "Error updating bandwidth chart",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
        }
    }

    void HandleTrafficRateUpdated(object? sender, NetworkTraffic traffic)
    {
        var (inMegabits, outMegabits) = traffic.AsMegabits();
        this.PlotModel.AddTraffic(inMegabits, outMegabits);
        this.Dispatcher?.Invoke(() => this.PlotModel.InvalidatePlot(true));
    }
}
