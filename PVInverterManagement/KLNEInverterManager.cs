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
    public class KLNEInverterManager : PhoenixtecInverterManager2
    {
        public override String InverterManagerType { get { return "KLNE"; } }
        public override String ConversationFileName { get { return "KLNE_Conversations_v1.txt"; } }

        public KLNEInverterManager(GenThreadManagement.GenThreadManager genThreadManager, int imid,
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
                value.EnergyToday = new DataInfo(0x0D, DataUnits.KiloWattHours);
                value.VoltsPV = new DataInfo(0x40, DataUnits.Volts);
                value.CurrentAC = new DataInfo(0x41, DataUnits.Amps);
                value.VoltsAC = new DataInfo(0x42, DataUnits.Volts);
                value.FreqAC = new DataInfo(0x43, DataUnits.Hertz, 0.01);
                value.PowerAC = new DataInfo(0x44, DataUnits.Watts, 1.0);
                value.ImpedanceAC = new DataInfo(0x45, DataUnits.mOhms, 1.0);
                value.EnergyTotalHigh = new DataInfo(0x47, DataUnits.KiloWattHours);
                value.EnergyTotalLow = new DataInfo(0x48, DataUnits.KiloWattHours);
                value.HoursHigh = new DataInfo(0x49, DataUnits.Hours, 1.0);
                value.HoursLow = new DataInfo(0x4A, DataUnits.Hours, 1.0);
                value.Mode = new DataInfo(0x4C, DataUnits.Identifier, 1.0);

                value.VoltsPV1 = new DataInfo(0x01, DataUnits.Volts);
                value.VoltsPV2 = new DataInfo(0x02, DataUnits.Volts);
                value.VoltsPV3 = new DataInfo(0x03, DataUnits.Volts);
                value.CurrentPV1 = new DataInfo(0x04, DataUnits.Amps);
                value.CurrentPV2 = new DataInfo(0x05, DataUnits.Amps);
                value.CurrentPV3 = new DataInfo(0x06, DataUnits.Amps);
                value.ErrorGV = new DataInfo(0x78, DataUnits.Identifier, 1.0);
                value.ErrorGF = new DataInfo(0x79, DataUnits.Identifier, 1.0);
                value.ErrorGZ = new DataInfo(0x7A, DataUnits.Identifier, 1.0);
                value.ErrorTemp = new DataInfo(0x7B, DataUnits.Identifier, 1.0);
                value.ErrorPV1 = new DataInfo(0x7C, DataUnits.Identifier, 1.0);
                value.ErrorGFC1 = new DataInfo(0x7D, DataUnits.Identifier, 1.0);
                value.ErrorModeLow = new DataInfo(0x7E, DataUnits.Identifier, 1.0);

                return value;
            }
        }

        protected override DataIds CustomInitialise()
        {
            // For KLNE this will ignore any position settings established in 
            // the inverter data format message exchange (doe not work for KLNE)
            DataIds value = InverterDataIds;

            value.Temp.Position = 0;
            value.VoltsPV.Position = 2;
            value.CurrentAC.Position = 4;
            value.VoltsAC.Position = 6;
            value.FreqAC.Position = 8;
            value.PowerAC.Position = 10;
            value.EnergyToday.Position = 12;
            value.EnergyTotalHigh.Position = 14;
            value.EnergyTotalLow.Position = 16;
            value.HoursHigh.Position = 18;
            value.HoursLow.Position = 20;
            
            // I suspect following is some type of status report - seems to be always 12545
            // 12545 is 0x1101
            value.Mode.Position = 22;

            value.ErrorGV.Position = 24;
            value.ErrorGF.Position = 26;
            value.ErrorGZ.Position = 28;
            value.ErrorTemp.Position = 30;
            value.ErrorPV1.Position = 32;
            value.ErrorGFC1.Position = 34;
            value.ErrorModeLow.Position = 36;

            return value;
        }
    }
}
