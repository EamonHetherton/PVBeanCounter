/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using GenThreadManagement;
using Conversations;
using DeviceStream;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class SMABluetoothInverterManager : InverterManager
    {
        public override String InverterManagerType
        {
            get
            {
                return "SMA Bluetooth";
            }
        }

        SMABluetooth_Converse Converse;

        private string ConversationConfigFile;
        //private BluetoothStream Stream;

        public SMABluetoothInverterManager(GenThreadManager genThreadManager, 
            InverterManagerSettings imSettings, IManagerManager ManagerManager)
            : base(genThreadManager, imSettings, ManagerManager)
        {
            ConversationConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SMABluetooth_Conversations.txt");

            Converse = new SMABluetooth_Converse(GlobalSettings.SystemServices);
            ConversationLoader conversations = new ConversationLoader(ConversationConfigFile, GlobalSettings.SystemServices);
            conversations.LoadConversations(Converse);

            //Stream = new BluetoothStream(settings.SMABluetoothID, utilityLog);
            //Converse.SetDeviceStream(Stream);
        }

        protected int ExtractYield(PVHistoryUpdate historyUpdater)
        {
            throw new NotImplementedException();
        }

        public override TimeSpan Interval { get { return TimeSpan.FromSeconds(300); } }

        public override void DoInverterWork()
        {
            //RunExtract(true);
        }

        public void RunExtract(bool runContinuous)
        {
            String state = "start";
            PVHistoryUpdate historyUpdater = new PVHistoryUpdate(InverterDataRecorder);

            Continuous = runContinuous;

            DateTime time = DateTime.Now;
            int shortCount = 0;
            const int shortSleep = 90;
            const int minSleep = 10;

            state = "before do";

            LogMessage("RunExtract", "Inverter Manager - Id = " + InverterManagerID + " - manager running", LogEntryType.Information);
            try
            {
                do
                {
                    TimeSpan? inverterStart = GlobalSettings.ApplicationSettings.InverterStartTime;
                    TimeSpan? inverterStop = GlobalSettings.ApplicationSettings.InverterStopTime;

                    if ((inverterStop < DateTime.Now.TimeOfDay)
                    || (inverterStart > DateTime.Now.TimeOfDay))
                    {
                        Thread.Sleep(60000);
                        continue;
                    }

                    int res = 0;
                    DateTime startTime = DateTime.Now;

                    state = "before GetMutex";
                    GlobalSettings.SystemServices.GetDatabaseMutex();
                    try
                    {
                        res = ExtractYield(historyUpdater);

                        /*
                        state = "before SetPVOutputReady";
                        if (res > 0)
                            SetPVOutputReady();
                        */
                    }
                    catch (Exception e)
                    {
                        LogMessage("RunExtract", "Status: " + state + " - exception: " + e.Message, LogEntryType.ErrorMessage);
                        //throw (e);
                    }
                    finally
                    {
                        GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                    }

                    if (!Continuous)
                        break;
                    long longSleep = ((((int)(DateTime.Now.TimeOfDay.TotalMinutes / 5)) * 5 + 6) * 60 - (int)DateTime.Now.TimeOfDay.TotalSeconds);
                    //long longSleep = Frequency - (DateTime.Now - startTime).Seconds;
                    if (longSleep < minSleep)
                        longSleep = minSleep; // ensure that it does sleep a bit

                    if ((res > 0) || (shortCount > 20) || (longSleep < shortSleep))
                    {   // if data found - sleep for Frequency seconds
                        // if already used long/short short sleeps, use a Frequency sleep
                        // ignore shortSleep if Frequency is set to less than shortSleep
                        Thread.Sleep((Int32)(longSleep * 1000));
                        if (res > 0)
                            shortCount = 0;
                    }
                    else
                    {   //sleep for 60 seconds if already slept for Frequency seconds - 
                        Thread.Sleep((Int32)(shortSleep * 1000));
                        shortCount++;
                    }
                    //Thread.Sleep(10000);
                }
                while (Continuous);
            }
            catch (System.Threading.ThreadInterruptedException)
            {
                // LogMessage("RunExtract - Inverter Manager - Id = " + InverterManagerID + " - interrupted", LogEntryType.ErrorMessage);
                historyUpdater = null;
            }
            LogMessage("RunExtract", "Inverter Manager - Id = " + InverterManagerID + " - manager stopping", LogEntryType.StatusChange);
        }

    }
}
