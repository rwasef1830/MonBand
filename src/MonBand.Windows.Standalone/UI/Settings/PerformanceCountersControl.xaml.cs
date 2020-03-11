using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using MonBand.Core;
using MonBand.Core.PerformanceCounters;
using MonBand.Windows.Infrastructure.Input;
using MonBand.Windows.Models;
using MonBand.Windows.Models.Settings;

namespace MonBand.Windows.Standalone.UI.Settings
{
    partial class PerformanceCountersControl
    {
        public static readonly DependencyProperty LoggerFactoryProperty = DependencyProperty.Register(
            nameof(LoggerFactory),
            typeof(ILoggerFactory),
            typeof(PerformanceCountersControl));

        public static readonly DependencyProperty PollersProperty = DependencyProperty.Register(
            nameof(Pollers),
            typeof(ObservableCollection<PerformanceCounterPollerConfig>),
            typeof(PerformanceCountersControl));

        ITrafficRateService _trafficRateService;

        public ILoggerFactory LoggerFactory
        {
            get => (ILoggerFactory)this.GetValue(LoggerFactoryProperty);
            set => this.SetValue(LoggerFactoryProperty, value);
        }

        public ObservableCollection<PerformanceCounterPollerConfig> Pollers
        {
            get => (ObservableCollection<PerformanceCounterPollerConfig>)this.GetValue(PollersProperty);
            set => this.SetValue(PollersProperty, value);
        }

        public ICommand AddMonitor { get; }
        public ICommand DeleteMonitor { get; }
        public BandwidthPlotModel PlotModel { get; }
        public IList<string> InterfaceNames { get; }

        public PerformanceCountersControl()
        {
            this.AddMonitor = new DelegateCommand(
                _ => this.Pollers.Add(
                    new PerformanceCounterPollerConfig { InterfaceName = "<unknown>" }));
            this.DeleteMonitor = new DelegateCommand(
                o => this.Pollers.Remove((PerformanceCounterPollerConfig)o));
            this.PlotModel = new BandwidthPlotModel(100);
            this.InterfaceNames = PerformanceCounterInterfaceQuery.GetInterfaceNames();

            this.InitializeComponent();
            this.Loaded += this.HandleLoaded;
        }

        void HandleLoaded(object sender, RoutedEventArgs e)
        {
            if (this.LoggerFactory == null && !DesignerProperties.GetIsInDesignMode(this))
            {
                throw new InvalidOperationException($"{nameof(this.LoggerFactory)} must be set.");
            }
        }

        void HandleListBoxMonitorsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.GridMonitorForm.Visibility = e.AddedItems.Count == 0 ? Visibility.Hidden : Visibility.Visible;

            foreach (PerformanceCounterPollerConfig config in e.RemovedItems)
            {
                config.PropertyChanged -= this.HandleConfigOnPropertyChanged;
            }

            foreach (PerformanceCounterPollerConfig config in e.AddedItems)
            {
                config.PropertyChanged += this.HandleConfigOnPropertyChanged;
                this.UpdateTrafficRateService(config);
            }
        }

        void HandleConfigOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.UpdateTrafficRateService((PerformanceCounterPollerConfig)sender);
        }

        void UpdateTrafficRateService(PerformanceCounterPollerConfig config)
        {
            try
            {
                var newTrafficRateService = new PerformanceCounterTrafficRateService(
                    config.InterfaceName,
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
                MessageBox.Show(
                    Window.GetWindow(this),
                    ex.Message,
                    "Error updating bandwidth chart",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
        }

        void HandleTrafficRateUpdated(object sender, NetworkTraffic traffic)
        {
            var megabits = traffic.AsMegabits();
            this.PlotModel.AddTraffic(megabits.InMegabits, megabits.OutMegabits);
            this.Dispatcher?.Invoke(() => this.PlotModel.InvalidatePlot(true));
        }
    }
}
