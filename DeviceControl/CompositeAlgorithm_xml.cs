/*
* Copyright (c) 2013 Dennis Mackay-Fisher
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
* along with PV Bean Counter.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Algorithms;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;
using Conversations;

namespace Device
{
    public class CompositeAlgorithm_xml : DeviceAlgorithm
    {
        public DynamicByteVar MessageData { get; private set; }
        // Add String Variables below

        public String Message { get; private set; }
        
        // public void SetMessage(string value) { Message = value; }
        
        // End String Variables

        // Add Numeric Variables below


        // End Numeric Variables

        // Add Bytes Variables below


        // End Bytes Variables


        public CompositeAlgorithm_xml(AlgorithmParams algorithmParams)
            : base(algorithmParams)
        {
        }

        public CompositeAlgorithm_xml(DeviceManagerDeviceSettings deviceSettings, Protocol protocol, ErrorLogger errorLogger)
            : base(deviceSettings, protocol, errorLogger)
        {
        }

        protected override void LoadVariables()
        {
            MessageData = (DynamicByteVar)Params.Protocol.GetSessionVariable("Message", null);
        }

        public override void ClearAttributes()
        {
            Message = "";
        }

        public bool ExtractReading(bool dbWrite, ref bool alarmFound, ref bool errorFound)
        {
            bool res = false;
            if (FaultDetected)
                return res;

            String stage = "Reading";
            try
            {
                res = LoadBlockType("Reading", true, true, ref alarmFound, ref errorFound);
                Message = MessageData.ToString();
            }
            catch (Exception e)
            {
                LogMessage("DoExtractReadings - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            return res;
        }

    }

    public class CompositeAlgorithm_EW4009 : DeviceAlgorithm
    {
        // Add String Variables below

        /*
        public void SetStatus01(string value) { Readings[0].Status = value; }
        public void SetStatus02(string value) { Readings[1].Status = value; }
        public void SetStatus03(string value) { Readings[2].Status = value; }
        public void SetStatus04(string value) { Readings[3].Status = value; }
        public void SetStatus05(string value) { Readings[4].Status = value; }
        public void SetStatus06(string value) { Readings[5].Status = value; }
        public void SetStatus07(string value) { Readings[6].Status = value; }
        public void SetStatus08(string value) { Readings[7].Status = value; }
        public void SetStatus09(string value) { Readings[8].Status = value; }
        public void SetStatus10(string value) { Readings[9].Status = value; }
        public void SetStatus11(string value) { Readings[10].Status = value; }
        public void SetStatus12(string value) { Readings[11].Status = value; }
        public void SetStatus13(string value) { Readings[12].Status = value; }
        public void SetStatus14(string value) { Readings[13].Status = value; }
        public void SetStatus15(string value) { Readings[14].Status = value; }
        public void SetStatus16(string value) { Readings[15].Status = value; }
        */

        // public void SetMessage(string value) { Message = value; }

        // End String Variables

        // Add Numeric Variables below
        /*
        public decimal? Power01 { get; private set; }
        public decimal? Power02 { get; private set; }
        public decimal? Power03 { get; private set; }
        public decimal? Power04 { get; private set; }
        public decimal? Power05 { get; private set; }
        public decimal? Power06 { get; private set; }
        public decimal? Power07 { get; private set; }
        public decimal? Power08 { get; private set; }
        public decimal? Power09 { get; private set; }
        public decimal? Power10 { get; private set; }
        public decimal? Power11 { get; private set; }
        public decimal? Power12 { get; private set; }
        public decimal? Power13 { get; private set; }
        public decimal? Power14 { get; private set; }
        public decimal? Power15 { get; private set; }
        public decimal? Power16 { get; private set; }
        */
        /*
        public void SetPower01(decimal value) { Readings[0].Power = value; }
        public void SetPower02(decimal value) { Readings[1].Power = value; }
        public void SetPower03(decimal value) { Readings[2].Power = value; }
        public void SetPower04(decimal value) { Readings[3].Power = value; }
        public void SetPower05(decimal value) { Readings[4].Power = value; }
        public void SetPower06(decimal value) { Readings[5].Power = value; }
        public void SetPower07(decimal value) { Readings[6].Power = value; }
        public void SetPower08(decimal value) { Readings[7].Power = value; }
        public void SetPower09(decimal value) { Readings[8].Power = value; }
        public void SetPower10(decimal value) { Readings[9].Power = value; }
        public void SetPower11(decimal value) { Readings[10].Power = value; }
        public void SetPower12(decimal value) { Readings[11].Power = value; }
        public void SetPower13(decimal value) { Readings[12].Power = value; }
        public void SetPower14(decimal value) { Readings[13].Power = value; }
        public void SetPower15(decimal value) { Readings[14].Power = value; }
        public void SetPower16(decimal value) { Readings[15].Power = value; }
        */
        // End Numeric Variables

        // Add Bytes Variables below


        // End Bytes Variables

        public class DeviceReading
        {
            public decimal? Power { get; set; }
            public String Status { get; set; }

            public DeviceReading()
            {
                Power = null;
                Status = " ";
            }

            public void SetPower(decimal value) { Power = value; }
            public void SetStatus(string value) { Status = value; }
        }

        private DeviceReading[] Readings;

        public DeviceReading GetReading(int address)
        {
            if (address < 1)
                return null;
            if (address > 16)
                return null;
            return Readings[address - 1];
        }

        public CompositeAlgorithm_EW4009(AlgorithmParams algorithmParams)
            : base(algorithmParams)
        {
            Readings = new DeviceReading[16];
            for (int i = 0; i < 16; i++)
            {
                Readings[i] = new DeviceReading();
            }
        }

        public CompositeAlgorithm_EW4009(DeviceManagerDeviceSettings deviceSettings, Protocol protocol, ErrorLogger errorLogger)
            : base(deviceSettings, protocol, errorLogger)
        {
            Readings = new DeviceReading[16];
            for (int i = 0; i < 16; i++)
            {
                Readings[i] = new DeviceReading();
            }
        }

        protected override void LoadVariables()
        {
            VariableEntry var;
            /*
            var = new VariableEntry_Numeric("Power01", SetPower01);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power02", SetPower02);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power03", SetPower03);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power04", SetPower04);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power05", SetPower05);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power06", SetPower06);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power07", SetPower07);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power08", SetPower08);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power09", SetPower09);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power10", SetPower10);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power11", SetPower11);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power12", SetPower12);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power13", SetPower13);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power14", SetPower14);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power15", SetPower15);
            VariableEntries.Add(var);
            var = new VariableEntry_Numeric("Power16", SetPower16);
            VariableEntries.Add(var);

            var = new VariableEntry_String("Status01", SetStatus01);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status02", SetStatus02);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status03", SetStatus03);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status04", SetStatus04);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status05", SetStatus05);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status06", SetStatus06);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status07", SetStatus07);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status08", SetStatus08);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status09", SetStatus09);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status10", SetStatus10);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status11", SetStatus11);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status12", SetStatus12);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status13", SetStatus13);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status14", SetStatus14);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status15", SetStatus15);
            VariableEntries.Add(var);
            var = new VariableEntry_String("Status16", SetStatus16);
            */
            for (int i = 0; i < 16; i++)
            {
                int j = i + 1;
                var = new VariableEntry_Numeric("Power" + j.ToString("00"), Readings[i].SetPower);
                VariableEntries.Add(var);
                var = new VariableEntry_String("Status" + j.ToString("00"), Readings[i].SetStatus);
                VariableEntries.Add(var);
            }
            
        }

        public override void ClearAttributes()
        {
            for (int i = 0; i < 16; i++)
            {
                Readings[i].Power = null;
                Readings[i].Status = " ";
            }
        }

        public bool ExtractReading(bool dbWrite, ref bool alarmFound, ref bool errorFound)
        {
            bool res = false;
            if (FaultDetected)
                return res;

            String stage = "Reading";
            try
            {
                res = LoadBlockType("Reading", true, true, ref alarmFound, ref errorFound);
                
            }
            catch (Exception e)
            {
                LogMessage("DoExtractReadings - Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                return false;
            }

            return res;
        }

    }
}
