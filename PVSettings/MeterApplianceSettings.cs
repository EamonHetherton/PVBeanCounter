/*
* Copyright (c) 2011 Dennis Mackay-Fisher
*
* This file is part of PV Bean Counter
* 
* PV Bean Counter is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PV Bean Counter is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace PVSettings
{
    public class MeterApplianceSettings : SettingsBase
    {
        MeterManagerSettings.MeterManagerType meterTypeInternal;

        public MeterApplianceSettings(SettingsBase root, XmlElement element, 
            MeterManagerSettings.MeterManagerType meterType)
            : base(root, element)
        {
            meterTypeInternal = meterType;
        }

        public MeterManagerSettings.MeterManagerType MeterType { get { return meterTypeInternal; } }

        public String ApplianceNo
        {
            get
            {
                String rffd = GetValue("applianceno");
                return rffd;
            }

            set
            {
                SetValue("applianceno", value, "ApplianceNo");
            }
        }

        public bool IsConsumption
        {
            get
            {
                String rffd = GetValue("consumptionsiteid");
                if (rffd != "" && rffd != null)
                    return true;
                else
                    return false;
            }
        }

        public bool IsInverterYield
        {
            get
            {
                String rffd = GetValue("inverter");
                if (rffd != "" && rffd != null)
                    return true;
                else
                    return false;
            }
        }

        public String ConsumptionSiteId
        {
            get
            {
                return GetValue("consumptionsiteid");
            }

            set
            {
                SetValue("consumptionsiteid", value, "ConsumptionSiteId");
                if (value != "")
                {
                    StoreReading = true;
                    SetValue("inverter", "", "Inverter");
                }
            }
        }

        public String Inverter
        {
            get
            {
                return GetValue("inverter");
            }

            set
            {
                SetValue("inverter", value, "Inverter");
                if (value != "")
                {
                    StoreReading = true;
                    SetValue("consumptionsiteid", "", "ConsumptionSiteId");
                }
            }
        }

        public bool StoreReading
        {
            get
            {
                String rffd = GetValue("storereading");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value || IsConsumption || IsInverterYield)
                {
                    SetValue("storereading", "true", "StoreReading");
                }
                else
                {
                    SetValue("storereading", "", "StoreReading");
                }
            }
        }

        public bool AdjustHistory
        {
            get
            {
                String rffd = GetValue("adjusthistory");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                {
                    SetValue("adjusthistory", "true", "AdjustHistory");
                }
                else
                {
                    SetValue("adjusthistory", "", "AdjustHistory");
                }
            }
        }

        public bool StoreHistory
        {
            get
            {
                String rffd = GetValue("storehistory");
                if (rffd == "true")
                    return true;
                else
                    return false;
            }

            set
            {
                if (value)
                {
                    SetValue("storehistory", "true", "StoreHistory");
                }
                else
                {
                    SetValue("storehistory", "", "StoreHistory");
                }
            }
        }

        public Double Calibrate
        {
            get
            {
                String rffd = GetValue("calibrate");

                if (rffd == "")
                    return 1.0;

                return Convert.ToDouble(rffd);
            }

            set
            {
                SetValue("calibrate", value.ToString(), "Calibrate");
            }
        }
    }
}
