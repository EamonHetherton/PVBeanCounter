/*
* Copyright (c) 2012 Dennis Mackay-Fisher
*
* This file is part of PV Scheduler
* 
* PV Scheduler is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PV Scheduler is distributed in the hope that it will be useful,
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
using GenericConnector;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;
using DeviceStream;
using GenThreadManagement;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class SAMILInverterManager : PhoenixtecInverterManager2
    {
        public override String InverterManagerType { get { return "SAMIL"; } }
        public override String ThreadName { get { return "SAMILInverterManager"; } }
        public override String ConversationFileName { get { return "SAMIL_Conversations_v1.txt"; } }

        public SAMILInverterManager(GenThreadManagement.GenThreadManager genThreadManager, int imid,
            InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager)
        {
        }

        protected override DataIds InverterDataIds
        {
            get
            {
                DataIds value = new DataIds(0);
                
                value.Temp = new DataInfo(0x00, DataUnits.DegreesCentegrade);
                //value.EnergyToday = new DataInfo(0x0C, DataUnits.KiloWattHours, 0.01);
                value.EnergyToday = new DataInfo(0x11, DataUnits.KiloWattHours, 0.01);
                //value.VoltsPV = new DataInfo(0x40, DataUnits.Volts);
                value.CurrentAC = new DataInfo(0x31, DataUnits.Amps);
                value.VoltsAC = new DataInfo(0x32, DataUnits.Volts);
                value.FreqAC = new DataInfo(0x33, DataUnits.Hertz, 0.01);
                value.PowerAC = new DataInfo(0x34, DataUnits.Watts, 1.0);
                //value.ImpedanceAC = new DataInfo(0x45, DataUnits.mOhms, 1.0);
                //value.EnergyTotalHigh = new DataInfo(0x09, DataUnits.KiloWattHours);
                value.EnergyTotalHigh = new DataInfo(0x35, DataUnits.KiloWattHours);
                value.EnergyTotalLow = new DataInfo(0x36, DataUnits.KiloWattHours);
                //value.HoursHigh = new DataInfo(0x12, DataUnits.Hours, 1.0);
                value.HoursHigh = new DataInfo(0x09, DataUnits.Hours, 1.0);
                value.HoursLow = new DataInfo(0x0A, DataUnits.Hours, 1.0);
                //value.Mode = new DataInfo(0x11, DataUnits.Identifier, 1.0);
                value.Mode = new DataInfo(0x0C, DataUnits.Identifier, 1.0);

                value.VoltsPV1 = new DataInfo(0x01, DataUnits.Volts);
                value.VoltsPV2 = new DataInfo(0x02, DataUnits.Volts);
                value.VoltsPV3 = new DataInfo(0x03, DataUnits.Volts);
                value.CurrentPV1 = new DataInfo(0x04, DataUnits.Amps);
                value.CurrentPV2 = new DataInfo(0x05, DataUnits.Amps);
                value.CurrentPV3 = new DataInfo(0x06, DataUnits.Amps);
                //value.ErrorGV = new DataInfo(0x13, DataUnits.Identifier, 1.0);
                value.ErrorGV = new DataInfo(0x37, DataUnits.Identifier, 1.0);
                //value.ErrorGF = new DataInfo(0x16, DataUnits.Identifier, 1.0);
                value.ErrorGF = new DataInfo(0x38, DataUnits.Identifier, 1.0);
                //value.ErrorGZ = new DataInfo(0x17, DataUnits.Identifier, 1.0);
                //value.ErrorTemp = new DataInfo(0x18, DataUnits.Identifier, 1.0);
                value.ErrorTemp = new DataInfo(0x12, DataUnits.Identifier, 1.0);
                //value.ErrorPV1 = new DataInfo(0x35, DataUnits.Identifier, 1.0);
                value.ErrorPV1 = new DataInfo(0x13, DataUnits.Identifier, 1.0);
                //value.ErrorGFC1 = new DataInfo(0x37, DataUnits.Identifier, 1.0);
                value.ErrorGFC1 = new DataInfo(0x16, DataUnits.Identifier, 1.0);
                value.ErrorModeHigh = new DataInfo(0x17, DataUnits.Identifier, 1.0);
                value.ErrorModeLow = new DataInfo(0x18, DataUnits.Identifier, 1.0);

                return value;
            }
        }       

    }
}
