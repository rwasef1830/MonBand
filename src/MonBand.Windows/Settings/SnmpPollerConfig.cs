using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MonBand.Windows.Settings
{
    public class SnmpPollerConfig : INotifyPropertyChanged
    {
        string _address;
        ushort _port;
        string _community;
        string _interfaceName;

        public string Address
        {
            get => this._address;
            set
            {
                if (this._address != value)
                {
                    this._address = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public ushort Port
        {
            get => this._port;
            set
            {
                if (this._port != value)
                {
                    this._port = value;
                    this.OnPropertyChanged();
                }
            }
        }

        public string Community
        {
            get => this._community;
            set
            {
                if (this._community != value)
                {
                    this._community = value;
                    this.OnPropertyChanged();
                }
            }
        }

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

        public override string ToString()
        {
            return $"{this.Address}:{this.Port} - {this.InterfaceName}";
        }
    }
}
