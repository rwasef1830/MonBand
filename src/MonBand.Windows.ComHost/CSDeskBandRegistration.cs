using System;
using System.Collections.Generic;

namespace MonBand.Windows.ComHost;

static class CSDeskBandRegistration
{
    public static readonly IReadOnlyDictionary<Type, CSDeskBandRegistrationAttribute> RegistrationsByType =
        new Dictionary<Type, CSDeskBandRegistrationAttribute>
        {
            [typeof(Deskband)] = new() { Name = "MonBand", ShowDeskBand = false }
        };
}
