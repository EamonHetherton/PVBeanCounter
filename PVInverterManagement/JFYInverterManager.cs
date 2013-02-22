/*
* Copyright (c) 2011 Dennis Mackay-Fisher
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
    public class JFYInverterManager : PhoenixtecInverterManager2, IConverseCheckSum16
    {
        public override String InverterManagerType { get { return "JFY"; } }
        public override String ConversationFileName { get { return "JFY_Conversations_v1.txt"; } }
            
        public JFYInverterManager(GenThreadManagement.GenThreadManager genThreadManager, int imid,
            InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imid, imSettings, ManagerManager)
        {
        }

        // IConverseCheckSum16 implementation

        protected override void LoadLocalConverse()
        {
            Converse = new Converse(GlobalSettings.SystemServices, this);
        }

        public UInt16 GetCheckSum16(List<byte[]> message)
        {
            /*
             Following is from the JFY Protocol document !!!
             CheckSum = Header + Source Address + Destination Address + Control Code + Function Code + Data length +Data0 + .. +Data (N-1) 
             (Add up and Reverse by bit,then add 1)
            */
            UInt16 checkSum = 0;

            // "Add up..."
            foreach (byte[] bytes in message)
            {
                ushort i;
                for (i = 0; i < bytes.Length; i++)
                {
                    checkSum += bytes[i];
                }
            }

            // assume " and Reverse by bit" means flip the bits
            checkSum = (UInt16)(checkSum ^ 0xFFFF);
            // "then add 1"
            checkSum += 1;

            return checkSum;
        }

        // End IConverseCheckSum16


        protected override bool HasPhoenixtecStartOfDayEnergyDefect { get { return true; } }

        protected override DataIds InverterDataIds
        {
            get
            {
                Double powerMultiplier = (DeviceList[0].Firmware.StartsWith("J1.") ? 1.0 : 0.1);  // Original spec stated 1.0;  inverters appeared that seem to use 0.1

                DataIds value = new DataIds(0);

                value.Temp = new DataInfo(0x00, DataUnits.DegreesCentegrade);
                value.VoltsPV1 = new DataInfo(0x01, DataUnits.Volts);
                value.VoltsPV2 = new DataInfo(0x02, DataUnits.Volts);
                value.VoltsPV3 = new DataInfo(0x03, DataUnits.Volts);
                value.CurrentPV1 = new DataInfo(0x04, DataUnits.Amps);
                value.CurrentPV2 = new DataInfo(0x05, DataUnits.Amps);
                value.CurrentPV3 = new DataInfo(0x06, DataUnits.Amps);
                value.EnergyTotalHigh = new DataInfo(0x47, DataUnits.KiloWattHours, 0.1, 0x07);
                value.EnergyTotalLow = new DataInfo(0x48, DataUnits.KiloWattHours, 0.1, 0x08);
                value.HoursHigh = new DataInfo(0x49, DataUnits.Hours, 1.0, 0x09);
                value.HoursLow = new DataInfo(0x4A, DataUnits.Hours, 1.0, 0x0A);
                value.PowerAC = new DataInfo(0x44, DataUnits.Watts, powerMultiplier, 0x0B);   // Refer to Issues 267 and 268
                value.Mode = new DataInfo(0x4C, DataUnits.Identifier, 1.0, 0x0C);                
                value.EnergyToday = new DataInfo(0x0D, DataUnits.KiloWattHours, 0.01);

                value.VoltsPV = new DataInfo(0x40, DataUnits.Volts);
                value.CurrentAC = new DataInfo(0x41, DataUnits.Amps);
                value.VoltsAC = new DataInfo(0x42, DataUnits.Volts);
                value.FreqAC = new DataInfo(0x43, DataUnits.Hertz, 0.01, 0x0B);
                value.ImpedanceAC = new DataInfo(0x45, DataUnits.mOhms, 1.0);
                
                
                value.ErrorGV = new DataInfo(0x78, DataUnits.Identifier, 1.0);
                value.ErrorGF = new DataInfo(0x79, DataUnits.Identifier, 1.0);
                value.ErrorGZ = new DataInfo(0x7A, DataUnits.Identifier, 1.0);
                value.ErrorTemp = new DataInfo(0x7B, DataUnits.Identifier, 1.0);
                value.ErrorPV1 = new DataInfo(0x7C, DataUnits.Identifier, 1.0);
                value.ErrorGFC1 = new DataInfo(0x7D, DataUnits.Identifier, 1.0);
                value.ErrorModeHigh = new DataInfo(0x7E, DataUnits.Identifier, 1.0);
                value.ErrorModeLow = new DataInfo(0x7F, DataUnits.Identifier, 1.0);

                return value;
            }
        }
    }
}
