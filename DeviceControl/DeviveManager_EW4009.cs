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
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using Conversations;
using DeviceStream;
using DeviceDataRecorders;
using GenThreadManagement;
using PVBCInterfaces;
using Algorithms;
using Device;

namespace DeviceControl
{

    public class DeviceManager_EW4009 : DeviceManager_PassiveController<EW4009_Device, EW4009_LiveRecord, EW4009_LiveRecord, CC128ManagerParams>
    {
        private CC128ManagerParams ManagerParams;

        private CompositeAlgorithm_EW4009 DeviceAlgorithm;
        private int DatabaseInterval;

        public DeviceManager_EW4009(GenThreadManager genThreadManager, DeviceManagerSettings mmSettings,
            IDeviceManagerManager imm)
            : base(genThreadManager, mmSettings, imm)
        {
            AlgorithmParams aParams;
            aParams.Protocol = Protocol;
            aParams.EndianConverter16Bit = Protocol.EndianConverter16Bit;
            aParams.EndianConverter32Bit = Protocol.EndianConverter32Bit;
            mmSettings.CheckListenerDeviceId();
            aParams.BlockList = mmSettings.ListenerDeviceSettings.BlockList;
            aParams.AlgorithmList = mmSettings.ListenerDeviceSettings.AlgorithmList;
            aParams.DeviceName = mmSettings.ListenerDeviceSettings.Description;
            aParams.ErrorLogger = ErrorLogger;
            DeviceAlgorithm = new CompositeAlgorithm_EW4009(aParams);

            DatabaseInterval = mmSettings.DBIntervalInt;
        }

        public override void Initialise()
        {
            base.Initialise();
        }

        protected override void LoadParams()
        {
            ManagerParams = new CC128ManagerParams();
            ManagerParams.DeviceType = PVSettings.DeviceType.EnergyMeter;
            ManagerParams.RecordingInterval = DeviceManagerSettings.DBIntervalInt;
            ManagerParams.QueryInterval = DeviceManagerSettings.MessageIntervalInt;
            ManagerParams.HistoryHours = DeviceManagerSettings.HistoryHours.Value;
        }

        protected override EW4009_Device NewDevice(DeviceManagerDeviceSettings dmDevice)
        {
            return new EW4009_Device(this, dmDevice);
        }

        public override bool DoWork()
        {
            bool alarmFound = false;
            bool errorFound = false;
            DateTime now = DateTime.Now;
            bool complete = false;
            try
            {
                if (DeviceAlgorithm.ExtractReading(true, ref alarmFound, ref errorFound))
                {
                    foreach (EW4009_Device dev in DeviceList)
                        if (dev.Enabled && dev.NextRunTime <= NextRunTimeStamp)
                        {
                            CompositeAlgorithm_EW4009.DeviceReading reading = DeviceAlgorithm.GetReading(dev.Address);
                            if (reading.Status == "1")
                            {
                                EW4009_LiveRecord rec;
                                rec.Watts = (int)reading.Power;
                                rec.TimeStampe = now;
                                dev.ProcessOneLiveReading(rec);
                            }
                            dev.LastRunTime = now;
                        }

                    complete = true;
                }                
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("DoWork", "Exception: " + e.Message);
                return false;
            }
            finally
            {
                if (!complete) // prevent rapid looping due to old LastRunTime
                    foreach (EW4009_Device dev in DeviceList)
                        if (dev.Enabled && dev.NextRunTime <= NextRunTimeStamp)
                            dev.LastRunTime = now;
            }

            return true;
        }
    }
}
