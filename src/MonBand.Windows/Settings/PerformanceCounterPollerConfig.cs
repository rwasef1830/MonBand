using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MonBand.Windows.Settings
{
    public class PerformanceCounterPollerConfig : INotifyPropertyChanged
    {
        string _interfaceName;

        public string InterfaceName
        {
            get => this._interfaceName;
            set
            {
                if (this._interfaceName != value)
                {
                    this._interfaceName = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
