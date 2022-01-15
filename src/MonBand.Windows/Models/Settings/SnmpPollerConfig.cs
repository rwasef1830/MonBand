using MonBand.Core.Util.Models;

namespace MonBand.Windows.Models.Settings
{
    public class SnmpPollerConfig : ObservableModelBase
    {
        string _address;
        ushort _port;
        string _community;
        string _interfaceName;

        public string Address
        {
            get => this._address;
            set => this.Set(ref this._address, value);
        }

        public ushort Port
        {
            get => this._port;
            set => this.Set(ref this._port, value);
        }

        public string Community
        {
            get => this._community;
            set => this.Set(ref this._community, value);
        }

        public string InterfaceName
        {
            get => this._interfaceName;
            set => this.Set(ref this._interfaceName, value);
        }
        
        public override string ToString()
        {
            return $"{this.Address}:{this.Port} - {this.InterfaceName}";
        }
    }
}
