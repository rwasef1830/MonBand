using MonBand.Core.Util.Models;

namespace MonBand.Windows.Models.Settings;

public class PerformanceCounterPollerConfig : ObservableModelBase
{
    string _interfaceName = string.Empty;

    public string InterfaceName
    {
        get => this._interfaceName;
        set => this.Set(ref this._interfaceName, value);
    }

    public override string ToString() => this._interfaceName;
}
