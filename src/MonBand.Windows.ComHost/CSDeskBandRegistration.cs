﻿using System;
using System.Collections.Generic;
using CSDeskBand;

namespace MonBand.Windows.ComHost
{
    static class CSDeskBandRegistration
    {
        public static readonly IReadOnlyDictionary<Type, CSDeskBandRegistrationAttribute> RegistrationsByType =
            new Dictionary<Type, CSDeskBandRegistrationAttribute>
            {
                [typeof(Deskband)] = new CSDeskBandRegistrationAttribute { Name = "MonBand", ShowDeskBand = false }
            };
    }
}
