using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace MonBand.Windows.UI.Helpers
{
    public static class ButtonHelper
    {
        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(ButtonHelper),
            new PropertyMetadata(CommandParameterChanged));

        static void CommandParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = d as ButtonBase;
            if (target == null)
                return;

            target.CommandParameter = e.NewValue;
            var temp = target.Command;

            // Have to set it to null first or CanExecute won't be called.
            target.Command = null;
            target.Command = temp;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static object GetCommandParameter(ButtonBase target)
        {
            return target.GetValue(CommandParameterProperty);
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void SetCommandParameter(ButtonBase target, object value)
        {
            target.SetValue(CommandParameterProperty, value);
        }
    }
}
