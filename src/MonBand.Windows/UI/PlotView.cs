using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OxyPlot.Wpf;

namespace MonBand.Windows.UI;

// Workaround https://github.com/oxyplot/oxyplot/pull/1820
public class PlotView : OxyPlot.Wpf.PlotView
{
    public static readonly DependencyProperty LoggerFactoryProperty = DependencyProperty.Register(
        nameof(LoggerFactory),
        typeof(ILoggerFactory),
        typeof(PlotView),
        new PropertyMetadata
        {
            DefaultValue = NullLoggerFactory.Instance,
            PropertyChangedCallback = (o, args) =>
            {
                ((PlotView)o)._logger = ((ILoggerFactory)args.NewValue).CreateLogger<PlotView>();
            }
        });

    ILogger _logger = NullLogger.Instance;

    public ILoggerFactory LoggerFactory
    {
        get => (ILoggerFactory)this.GetValue(LoggerFactoryProperty);
        set => this.SetValue(LoggerFactoryProperty, value);
    }

    static PlotView()
    {
        var harmony = new Harmony("OxyPlotBug1820Patch");
        var originalIsInVisualTree = AccessTools.Method(typeof(PlotViewBase), "IsInVisualTree");
        if (originalIsInVisualTree == null)
        {
            return;
        }

        var replacementIsInVisualTree = AccessTools.Method(typeof(PlotView), nameof(HasVisualAncestor));
        if (replacementIsInVisualTree == null)
        {
            throw new InvalidOperationException("BUG: Wrong patch method name specified");
        }

        harmony.Patch(originalIsInVisualTree, new HarmonyMethod(replacementIsInVisualTree));
    }

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention")]
    [SuppressMessage("ReSharper", "RedundantAssignment", Justification = "Harmony convention")]
    static bool HasVisualAncestor(PlotView __instance, ref bool __result)
    {
        DependencyObject? reference = __instance;
        while ((reference = VisualTreeHelper.GetParent(reference)) != null)
        {
            if (reference is not (Window or AdornerDecorator))
            {
                continue;
            }

            __instance._logger.LogTrace("Found render ancestor: {Type}", reference.GetType().FullName);
            __result = true;
            return false;
        }

        __instance._logger.LogTrace("Failed to find render ancestor");
        __result = false;
        return false;
    }
    
    protected override double UpdateDpi()
    {
        Matrix? transformToDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice;
        var dpi = transformToDevice.HasValue ? (transformToDevice.Value.M11 + transformToDevice.Value.M22) / 2.0 : 1.0;

        if (this.renderContext is not CanvasRenderContext context)
        {
            return dpi;
        }

        context.DpiScale = dpi;
        var ancestor = this.TryGetAncestorVisualFromVisualTree(this);
        if (ancestor != null)
        {
            context.VisualOffset = this.TransformToAncestor(ancestor).Transform(new Point());
        }

        return dpi;
    }

    Visual? TryGetAncestorVisualFromVisualTree(DependencyObject startElement)
    {
        var reference = startElement;
        while (true)
        {
            switch (reference)
            {
                case Window:
                case AdornerDecorator:
                case null:
                    this._logger.LogTrace("Search for ancestor yielded: {Result}", reference?.ToString());
                    return reference as Visual;
                
                default:
                    reference = VisualTreeHelper.GetParent(reference);
                    continue;
            }
        }
    }
}
