using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GenericConnector;
using MackayFisher.Utilities;
using PVBCInterfaces;
using Algorithms;
using PVSettings;
//using Device;

namespace DeviceDataRecorders
{
    public class DataRecorder : IDataRecorder
    {
        public IHistoryUpdate HistoryUpdater { get; set; }

        public virtual int IntervalSeconds { get { return 300; } } // Interval size in OutputHistory (not the thread run interval)

        private DateTime ProcessingDay;

        //private List<DeviceInfo> Devices;

        //public int InverterManagerID { get; private set; }

        private DateTime? NextFileDate = null;      // specifies the next DateTime to be used for extract

        private int PrevInterval = -1;

        private IManagerManager DeviceManagerManager;
        public IDeviceManager DeviceManager { get; private set; }

        public List<DeviceStatus> InverterStatusList { get; private set; }

        public DataRecorder(IDeviceManager deviceManager, DateTime? firstFullDay, bool useDefaultHistoryUpdater = true)
        {
            InverterStatusList = new List<DeviceStatus>();
            ProcessingDay = DateTime.MinValue;
            DeviceManagerManager = deviceManager.ManagerManager;
            DeviceManager = deviceManager;

            NextFileDate = firstFullDay;

            if (useDefaultHistoryUpdater)
                HistoryUpdater = (IHistoryUpdate)new HistoryUpdate(this, DeviceManagerManager.EnergyEvents);
            else
                HistoryUpdater = null;
        }

        private void ResetInverterDay(IDevice device)
        {
            //device.StartEnergyResolved = false;
            //device.EnergyDropFound = false;
            //device.UseEnergyTotal = true;
        }

        public bool CheckStartOfDay()
        {
            // returns true if starting a new day
            // returns false if continuing a day
            if (ProcessingDay != DateTime.Today)
            {
                DeviceManager.ResetStartOfDay();
                ProcessingDay = DateTime.Today;
                return true;
            }
            else
                return false;
        }

        private static String InsertDeviceReading =
            "INSERT INTO cmsdata " +
            "( Inverter_Id, OutputTime, EnergyTotal, EnergyToday, Temperature, VoltsPV, " +
            "VoltsPV1, VoltsPV2, VoltsPV3, CurrentPV1, CurrentPV2, CurrentPV3, " +
            "CurrentAC, VoltsAC, FrequencyAC, PowerAC, ImpedanceAC, Hours, Mode, " +
            "PowerPV, EstEnergy, ErrorCode) " +
            "VALUES " +
            "(@Inverter_Id, @OutputTime, @EnergyTotal, @EnergyToday, @Temperature, @VoltsPV, " +
            "@VoltsPV1, @VoltsPV2, @VoltsPV3, @CurrentPV1, @CurrentPV2, @CurrentPV3, " +
            "@CurrentAC, @VoltsAC, @FrequencyAC, @PowerAC, @ImpedanceAC, @Hours, @Mode, " +
            "@PowerPV, @EstEnergy, @ErrorCode)";

        public void RecordReading(IDevice device, DeviceEnergyReading reading)
        {
            string stage = "Init";
            GenConnection con = null;
            bool haveMutex = false;

            try
            {
                reading.EstEnergy = device.EstEnergy;

                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveMutex = true;
                con = GlobalSettings.TheDB.NewConnection();

                if (device.DeviceId == null)
                {
                    stage = "GetInverterId";
                    //String siteId;
                    /* DMF device.DeviceId = GetDeviceId(device.Manufacturer,
                                                device.Model,
                                                device.SerialNo, con, true, out siteId);
                    device.SiteId = siteId;
                    */
                }

                GenCommand cmd = new GenCommand(InsertDeviceReading, con);
                cmd.AddParameterWithValue("@Inverter_Id", device.DeviceId.Value);
                stage = "Time";
                cmd.AddParameterWithValue("@OutputTime", reading.Time);
                stage = "EnergyTotal";
                cmd.AddRoundedParameterWithValue("@EnergyTotal", reading.EnergyTotal, 3);
                stage = "EnergyToday";
                cmd.AddRoundedParameterWithValue("@EnergyToday", reading.EnergyToday, 3);
                stage = "Temp";
                cmd.AddRoundedParameterWithValue("@Temperature", reading.Temp, 2);
                stage = "VoltsPV";
                cmd.AddRoundedParameterWithValue("@VoltsPV", reading.VoltsPV, 2);
                stage = "VoltsPV1";
                cmd.AddRoundedParameterWithValue("@VoltsPV1", reading.VoltsPV1, 2);
                stage = "VoltsPV2";
                cmd.AddRoundedParameterWithValue("@VoltsPV2", reading.VoltsPV2, 2);
                stage = "VoltsPV3";
                cmd.AddRoundedParameterWithValue("@VoltsPV3", reading.VoltsPV3, 2);
                stage = "CurrentPV1";
                cmd.AddRoundedParameterWithValue("@CurrentPV1", reading.CurrentPV1, 2);
                stage = "CurrentPV2";
                cmd.AddRoundedParameterWithValue("@CurrentPV2", reading.CurrentPV2, 2);
                stage = "CurrentPV3";
                cmd.AddRoundedParameterWithValue("@CurrentPV3", reading.CurrentPV3, 2);
                stage = "CurrentAC";
                cmd.AddRoundedParameterWithValue("@CurrentAC", reading.CurrentAC, 2);
                stage = "VoltsAC";
                cmd.AddRoundedParameterWithValue("@VoltsAC", reading.VoltsAC, 2);
                stage = "FreqAC";
                cmd.AddRoundedParameterWithValue("@FrequencyAC", reading.FreqAC, 1);
                stage = "PowerPV";
                cmd.AddRoundedParameterWithValue("@PowerPV", reading.PowerPV, 2);
                stage = "PowerAC";
                cmd.AddRoundedParameterWithValue("@PowerAC", reading.PowerAC, 2);
                stage = "Mode";
                cmd.AddParameterWithValue("@Mode", reading.Mode);

                stage = "EstEnergy";
                // use rounding - 6 dp more than adequate - SQLite stores all values as text, too many digits wastes DB space
                cmd.AddRoundedParameterWithValue("@EstEnergy", reading.EstEnergy, 6);

                stage = "ErrorCode";
                if (reading.ErrorCode.HasValue)
                    cmd.AddParameterWithValue("@ErrorCode", (long)reading.ErrorCode.Value);
                else
                    cmd.AddParameterWithValue("@ErrorCode", null);

                stage = "ImpedanceAC";
                if (reading.ImpedanceAC.HasValue)
                    cmd.AddParameterWithValue("@ImpedanceAC", (long)reading.ImpedanceAC.Value);
                else
                    cmd.AddParameterWithValue("@ImpedanceAC", null);

                stage = "Hours";
                if (reading.Hours.HasValue)
                    cmd.AddParameterWithValue("@Hours", (long)reading.Hours.Value);
                else
                    cmd.AddParameterWithValue("@Hours", null);

                stage = "Execute";
                cmd.ExecuteNonQuery();

                if (GlobalSettings.SystemServices.LogTrace)
                    GlobalSettings.LogMessage("RecordReading", "EnergyTotal: " + reading.EnergyTotal +
                        " - EnergyToday: " + reading.EnergyToday +
                        " - PowerAC: " + reading.PowerAC +
                        " - Estimate: " + reading.EstEnergy, LogEntryType.Trace);

                //device.LastRecordTime = reading.Time;
                //device.EstEnergy = 0.0;
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("RecordReading", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
                if (haveMutex)
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
        }

        public List<DateTime> FindIncompleteDays(int inverterManagerID, bool resetFirstFullDay)         
        {             
            DateTime? startDate = NextFileDate;             
            List<DateTime> completeDays; 
             
            if (!resetFirstFullDay)                 
                completeDays = DeviceInfo.FindCompleteDays(inverterManagerID, startDate);             
            else                 
                completeDays = new List<DateTime>();              
            
            try             
            {                 
                // ensure we have a usable startDate                 
                if (startDate == null)                     
                    if (completeDays.Count > 0)                         
                        startDate = completeDays[0];                     
                    else                         
                        startDate = DateTime.Today;                  
                
                int numDays = (1 + (DateTime.Today - startDate.Value).Days);                 
                List<DateTime> incompleteDays = new List<DateTime>(numDays);                  
                
                for (int i = 0; i < numDays; i++)                 
                {                     
                    DateTime day = startDate.Value.AddDays(i);                      
                    if (!completeDays.Contains(day))                     
                    {                         
                        if (GlobalSettings.SystemServices.LogTrace)                             
                            GlobalSettings.SystemServices.LogMessage("FindInCompleteDays", "day: " + day, LogEntryType.Trace);                         
                        incompleteDays.Add(day);                     
                    }                 
                }                  
                return incompleteDays;             
            }              
            catch (Exception e)             
            {                 
                throw new Exception("FindIncompleteDays: error : " + e.Message, e);             
            }         
        } 

        private static List<DateTime> FindInverterCompleteDays(int inverterId, DateTime? startDate)
        {
            GenConnection connection = null;
            String cmdStr;
            GenCommand cmd;

            try
            {
                connection = GlobalSettings.TheDB.NewConnection();

                if (GlobalSettings.SystemServices.LogTrace && startDate != null)
                    GlobalSettings.SystemServices.LogMessage("FindInverterCompleteDays", "limit day: " + startDate, LogEntryType.Trace);

                // hack for SQLite - I suspect it does a string compare that results in startDate being excluded from the list
                // drop back 1 day for SQLite - the possibility of an extra day in this list does not damage the final result
                // (in incomplete days that is)
                if (connection.DBType == GenDBType.SQLite && startDate != null)
                {
                    startDate -= TimeSpan.FromDays(1);
                    if (GlobalSettings.SystemServices.LogTrace && startDate != null)
                        GlobalSettings.SystemServices.LogMessage("FindInverterCompleteDays", "SQLite adjusted limit day: " + startDate, LogEntryType.Trace);
                }

                // This implementation treats a day as complete if any inverter under the inverter manager reports a full day

                if (startDate == null)
                    cmdStr =
                    "select distinct oh.OutputDay " +
                    "from dayoutput_v oh " +
                    "where oh.Inverter_Id = @InverterId " +
                    "order by oh.OutputDay;";
                else
                    cmdStr =
                    "select distinct oh.OutputDay " +
                    "from dayoutput_v oh " +
                    "where oh.OutputDay >= @StartDate " +
                    "and oh.Inverter_Id = @InverterId " +
                    "order by oh.OutputDay;";

                cmd = new GenCommand(cmdStr, connection);
                if (startDate != null)
                    cmd.AddParameterWithValue("@StartDate", startDate);
                cmd.AddParameterWithValue("@InverterId", inverterId);
                GenDataReader dataReader = (GenDataReader)cmd.ExecuteReader();

                List<DateTime> dateList = new List<DateTime>(7);
                int cnt = 0;

                bool yesterdayFound = false;
                bool todayFound = false;
                DateTime today = DateTime.Today;
                DateTime yesterday = today.AddDays(-1);

                while (dataReader.Read())
                {
                    DateTime day = dataReader.GetDateTime(0);

                    yesterdayFound |= (day == yesterday);
                    todayFound |= (day == today);

                    if (day < yesterday)
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            GlobalSettings.SystemServices.LogMessage("FindInverterCompleteDays", "day: " + day, LogEntryType.Trace);
                        dateList.Add(dataReader.GetDateTime(0));
                        cnt++;
                    }
                }

                if (todayFound && yesterdayFound)
                    dateList.Add(yesterday);

                dataReader.Close();

                return dateList;
            }
            catch (Exception e)
            {
                throw new Exception("FindInverterCompleteDays: error executing query: " + e.Message, e);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        public List<DateTime> FindInverterIncompleteDays(IDevice device, bool ignoreDateReset = false)
        {
            DateTime? startDate = device.NextFileDate;
            List<DateTime> completeDays;

            if (!device.ResetFirstFullDay || ignoreDateReset)
                completeDays = FindInverterCompleteDays(device.DeviceId.Value, startDate);
            else
                completeDays = new List<DateTime>();

            try
            {
                // ensure we have a usable startDate
                if (startDate == null)
                    if (completeDays.Count > 0)
                        startDate = completeDays[0];
                    else
                        startDate = DateTime.Today;

                int numDays = (1 + (DateTime.Today - startDate.Value).Days);
                List<DateTime> incompleteDays = new List<DateTime>(numDays);

                for (int i = 0; i < numDays; i++)
                {
                    DateTime day = startDate.Value.AddDays(i);

                    if (!completeDays.Contains(day))
                    {
                        if (GlobalSettings.SystemServices.LogTrace)
                            GlobalSettings.SystemServices.LogMessage("FindInverterInCompleteDays", "day: " + day, LogEntryType.Trace);
                        incompleteDays.Add(day);
                    }
                }

                return incompleteDays;
            }

            catch (Exception e)
            {
                throw new Exception("FindInverterIncompleteDays: error : " + e.Message, e);
            }
        }

        private void UpdateOneOutputItem(EnergyReadingSet readingSet, DateTime currentIntervalTime,
            DateTime prevIntervalTime, Double energy, Int32? power, Int32 minPower, Int32 maxPower, Double? temperature)
        {
            EnergyReading reading;
            int intervalCount = ((int)(currentIntervalTime - prevIntervalTime).TotalSeconds) / IntervalSeconds;
            DateTime intervalTime = prevIntervalTime + TimeSpan.FromSeconds(IntervalSeconds);

            Double kwhOutput = Math.Round(energy / intervalCount, 4);

            if (!power.HasValue)
            {
                minPower = ((int)(kwhOutput * 3600 * 1000)) / IntervalSeconds;
                maxPower = minPower;
            }

            while (intervalTime <= currentIntervalTime)
            {
                reading = new EnergyReading();
                reading.Initialise(-1, intervalTime, 0, IntervalSeconds, null, null, false);

                reading.Seconds = IntervalSeconds;
                reading.EnergyDelta = kwhOutput;
                reading.MinPower = minPower;
                reading.MaxPower = maxPower;
                reading.Power = power;
                reading.Temperature = temperature;
                readingSet.Readings.Add(reading);
                intervalTime += TimeSpan.FromSeconds(IntervalSeconds);
            }
        }

        public void SetDeviceUpdated(int id, String siteId)
        {
            GlobalSettings.SystemServices.LogMessage("SetInverterUpdated", "id: " + id + " - siteId: " + siteId, LogEntryType.Trace);
            DeviceStatus status = LocateInverterStatus(id, siteId);
            status.LastOutput = DateTime.Now;
            if (siteId != null)
                DeviceManagerManager.SetPVOutputReady(siteId);
        }

        private static String SelectCMSData =
            "select OutputTime, EnergyTotal, EnergyToday, PowerAC, EstEnergy, Temperature " +
            "from cmsdata " +
            "where Inverter_Id = @InverterId " +
            "and OutputTime > @StartTime " +
            "and OutputTime <= @EndTime " +
            "order by OutputTime ";

        private void UpdateOneOutputHistory(IDevice device, DateTime day, bool useEnergyTotal, bool useDeltaAtStart)
        {
            String stage = "start";

            GenConnection con = null;
            EnergyReadingSet readingSet;

            try
            {
                GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "Updating day " + day + " - for inverter id " + device.DeviceId.Value, LogEntryType.Trace);

                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "Inverter";
                readingSet = new EnergyReadingSet(device, 100);

                stage = "new command";
                GenCommand cmd = new GenCommand(SelectCMSData, con);
                stage = "parameters";
                cmd.AddParameterWithValue("@InverterId", device.DeviceId.Value);
                cmd.AddParameterWithValue("@StartTime", day.Date);
                cmd.AddParameterWithValue("@EndTime", day.Date + TimeSpan.FromDays(1.0));

                stage = "execute";
                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();
                bool isFirst = true;

                Double prevInvEnergy = 0.0;
                Double currentInvEnergy = 0.0;
                Double currentInvDelta = 0.0;

                Double currentEnergyEstimate = 0.0;     // total of interval estimates for today
                Double prevEnergyEstimate = 0.0;     // previous total of interval estimates for today
                Double currentEstimateDelta = 0.0;

                Double energyRecorded = 0.0;
                Double prevEnergyRecorded = 0.0;

                Int32? power = null;
                Int32 minPower = 0;
                Int32 maxPower = 0;
                
                Double? temperature = null;
                DateTime prevIntervalTime = DateTime.Today;
                DateTime currentIntervalTime = DateTime.Today;

                stage = "enter loop";
                while (dr.Read())
                {
                    stage = "loop 1";
                    DateTime thisTime = dr.GetDateTime("OutputTime");
                    DateTime thisIntervalTime = thisTime.Date + TimeSpan.FromMinutes(((((int)thisTime.TimeOfDay.TotalMinutes) + 4) / 5) * 5);

                    Double thisInvEnergy = 0.0;

                    bool todayNull = dr.IsDBNull("EnergyToday");
                    Double estEnergy = dr.IsDBNull("EstEnergy") ? 0.0 : dr.GetDouble("EstEnergy");

                    if (useEnergyTotal)
                    {
                        if (dr.IsDBNull("EnergyTotal"))
                            GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "useEnergyTotal specified but not available - Time: " + thisIntervalTime, LogEntryType.ErrorMessage);
                        else
                        {
                            thisInvEnergy = dr.GetDouble("EnergyTotal");
                            useDeltaAtStart = true;     // no start of day value available - must use deltas only
                        }
                    }
                    else if (dr.IsDBNull("EnergyToday"))
                        GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "useEnergyTotal not specified but energy today not available - Time: " + thisIntervalTime, LogEntryType.ErrorMessage);
                    else
                        thisInvEnergy = dr.GetDouble("EnergyToday");

                    if (isFirst && useDeltaAtStart)
                    {
                        // first energy reading contains energy from previous days - must use deltas only from this point on
                        // CMS inverters with the start of day defect on this day and other inverters without EToday values start this way
                        isFirst = false;
                        prevInvEnergy = thisInvEnergy;
                        currentInvEnergy = thisInvEnergy;
                        prevEnergyEstimate = thisInvEnergy;
                        currentEnergyEstimate = thisInvEnergy;
                        currentIntervalTime = thisIntervalTime;
                        prevIntervalTime = currentIntervalTime - TimeSpan.FromSeconds(IntervalSeconds);
                    }
                    else
                    {
                        if (isFirst)
                        {
                            currentIntervalTime = thisIntervalTime;
                            prevIntervalTime = currentIntervalTime - TimeSpan.FromSeconds(IntervalSeconds);
                            isFirst = false;
                        }

                        if (currentInvEnergy < prevInvEnergy)
                        {
                            // Cannot report negative energy - try to preserve previous delta as part of next delta
                            if (useDeltaAtStart)
                            {
                                if (GlobalSettings.SystemServices.LogTrace)
                                    GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "Energy Today has reduced - was: " + prevInvEnergy.ToString() +
                                    " - now: " + currentInvEnergy.ToString() + " - Time: " + currentIntervalTime, LogEntryType.Trace);
                            }
                            else
                                GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "Energy Today has reduced - was: " + prevInvEnergy.ToString() +
                                    " - now: " + currentInvEnergy.ToString() + " - Time: " + currentIntervalTime, LogEntryType.ErrorMessage);

                            prevInvEnergy = currentInvEnergy - currentInvDelta;
                            currentEnergyEstimate = currentInvEnergy; // estimates are synced with inv values at energy reduction
                            prevEnergyEstimate = currentInvEnergy - currentEstimateDelta;
                            useDeltaAtStart = false; // activate estimate range checks and report energy reductions after the first on a day
                        }
                        else
                        {
                            currentInvDelta = currentInvEnergy - prevInvEnergy;
                            currentEstimateDelta = currentEnergyEstimate - prevEnergyEstimate;
                            // if a PVBC restart occurs energy estimate will be reset to 0 - retain previous estimate
                            // this is important with inverters using low resolution energy reporting
                            // missing estimates can cause a step shaped energy curve and peaked average power curve
                            if (currentEstimateDelta < 0.0)
                                currentEstimateDelta = estEnergy; // restart will cause at least one energy estimate value to be discarded - substitute the current estimate
                        }

                        if (thisIntervalTime != currentIntervalTime)
                        {
                            if (currentEnergyEstimate < currentInvEnergy)
                                currentEnergyEstimate = currentInvEnergy; // estimate lags - catchup
                            else if (currentEnergyEstimate > (currentInvEnergy + device.EstMargin))
                                currentEnergyEstimate = (currentInvEnergy + device.EstMargin);

                            currentEstimateDelta = currentEnergyEstimate - prevEnergyEstimate;
                            // if a PVBC restart occurs energy estimate will be reset to 0 - retain previous estimate
                            // this is important with inverters using low resolution energy reporting
                            // missing estimates can cause a step shaped energy curve and peaked average power curve
                            if (currentEstimateDelta < 0.0)
                                currentEstimateDelta = estEnergy; // restart will cause at least one energy estimate value to be discarded - substitute the current estimate

                            prevEnergyRecorded = energyRecorded;
                            energyRecorded += currentEstimateDelta;

                            UpdateOneOutputItem(readingSet, currentIntervalTime, prevIntervalTime,
                                currentEstimateDelta, power, minPower, maxPower, temperature);

                            prevInvEnergy = currentInvEnergy;
                            prevEnergyEstimate = currentEnergyEstimate;
                            prevIntervalTime = currentIntervalTime;
                            currentIntervalTime = thisIntervalTime;
                            currentInvEnergy = thisInvEnergy;

                            minPower = 0;
                            maxPower = 0;
                        }

                        currentInvEnergy = thisInvEnergy;
                        currentEnergyEstimate += estEnergy;
                    }

                    if (dr.IsDBNull("PowerAC"))
                        power = null;
                    else
                        power = dr.GetInt32("PowerAC");

                    if (power != null)
                    {
                        if (power.Value < minPower || minPower == 0)
                            minPower = power.Value;
                        if (power.Value > maxPower)
                            maxPower = power.Value;
                    }

                    if (dr.IsDBNull("Temperature"))
                        temperature = null;
                    else
                        temperature = dr.GetDouble("Temperature");
                }

                // write out last if it has an energy value
                if (currentEnergyEstimate > prevEnergyEstimate)
                    UpdateOneOutputItem(readingSet, currentIntervalTime, prevIntervalTime,
                        currentEnergyEstimate - prevEnergyEstimate, power, minPower, maxPower, temperature);

                stage = "end loop";
                dr.Close();
                dr.Dispose();
                dr = null;
                stage = "history update";

                GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "Day: " + day + " - count: " + readingSet.Readings.Count, LogEntryType.Trace);

                if (readingSet.Readings.Count > 0)
                    HistoryUpdater.UpdateReadingSet(readingSet, con, false);
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("UpdateOneOutputHistory", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
        }

        private bool? CheckForEnergyDrop(IDevice device, DateTime day, bool useEnergyTotal)
        {
            String stage = "start";
            bool? useDeltas = null;

            GenConnection con = null;

            try
            {
                GlobalSettings.SystemServices.LogMessage("CheckForEnergyDrop", "Day " + day + " - for inverter id " + device.DeviceId.Value, LogEntryType.Trace);

                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "new command";
                GenCommand cmd = new GenCommand(SelectCMSData, con);
                stage = "parameters";
                cmd.AddParameterWithValue("@InverterId", device.DeviceId.Value);
                cmd.AddParameterWithValue("@StartTime", day.Date);
                cmd.AddParameterWithValue("@EndTime", day.Date + TimeSpan.FromDays(1.0));

                stage = "execute";
                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();
                bool isFirst = true;

                Double prevInvEnergy = 0.0;
                DateTime firstIntervalTime = DateTime.Today;

                stage = "enter loop";
                while (dr.Read())
                {
                    stage = "loop 1";
                    DateTime thisIntervalTime = dr.GetDateTime("OutputTime");

                    Double thisInvEnergy = 0.0;
                    bool todayNull = dr.IsDBNull("EnergyToday");

                    if (useEnergyTotal)
                    {
                        if (!dr.IsDBNull("EnergyTotal"))
                            thisInvEnergy = dr.GetDouble("EnergyTotal");
                    }
                    else if (!dr.IsDBNull("EnergyToday"))
                        thisInvEnergy = dr.GetDouble("EnergyToday");

                    if (isFirst)
                    {
                        prevInvEnergy = thisInvEnergy;
                        firstIntervalTime = thisIntervalTime;
                        isFirst = false;
                    }
                    else if (thisInvEnergy < prevInvEnergy)
                    {
                        useDeltas = true;
                        break;
                    }
                    else if (thisIntervalTime - firstIntervalTime > TimeSpan.FromMinutes(device.CrazyDayStartMinutes))
                    {
                        useDeltas = false;
                        break;
                    }
                    else
                        prevInvEnergy = thisInvEnergy;
                }

                stage = "end loop";
                dr.Close();
                dr.Dispose();
                dr = null;

                GlobalSettings.SystemServices.LogMessage("CheckForEnergyDrop", "Day: " + day + " - result: " + useDeltas, LogEntryType.Trace);
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("CheckForEnergyDrop", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
            return useDeltas;
        }

        private static String SelectCMSDataCountToday =
            "select count(*) " +
            "from cmsdata " +
            "where Inverter_Id = @InverterId " +
            "and OutputTime > @StartTime " +
            "and OutputTime <= @EndTime " +
            "and EnergyToday > 0 ";

        private bool UseEnergyTotal(int deviceId, DateTime day)
        {
            String stage = "start";
            GenConnection con = null;

            try
            {
                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "new check command";
                GenCommand cmdCheck = new GenCommand(SelectCMSDataCountToday, con);
                stage = "check parameters";
                cmdCheck.AddParameterWithValue("@InverterId", deviceId);
                cmdCheck.AddParameterWithValue("@StartTime", day.Date);
                cmdCheck.AddParameterWithValue("@EndTime", day.Date + TimeSpan.FromDays(1.0));

                stage = "execute check";
                GenDataReader drCheck = (GenDataReader)cmdCheck.ExecuteReader();

                bool useTotal = true;

                stage = "read check";
                if (drCheck.Read())
                {
                    if (drCheck.IsDBNull(0))
                        useTotal = true;
                    else
                        useTotal = drCheck.GetInt32(0) == 0;
                }
                stage = "cleanup check";
                drCheck.Close();
                drCheck.Dispose();
                drCheck = null;
                cmdCheck.Dispose();
                cmdCheck = null;

                return useTotal;
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("UseEnergyToday", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }
            }
            return false;
        }

        private static String SelectAllInverters =
            "select i.Id, i.SerialNumber, i.SiteId " +
            "from inverter as i ";

        private void UpdateAllOutputHistory(DateTime day)
        {
            String stage = "start";

            GenConnection con = null;
            bool haveMutex = false;

            try
            {
                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveMutex = true;

                GlobalSettings.SystemServices.LogMessage("UpdateAllOutputHistory", "Updating day " + day, LogEntryType.Trace);
                stage = "new connection";
                con = GlobalSettings.TheDB.NewConnection();

                stage = "new command";
                GenCommand cmd = new GenCommand(SelectAllInverters, con);

                stage = "execute";
                GenDataReader dr = (GenDataReader)cmd.ExecuteReader();

                stage = "enter loop";
                while (dr.Read())
                {
                    IDevice device = null;
                    /* DMF
                    foreach (IDevice info in DeviceManager.DeviceList)
                        if (info.SerialNo == device.SerialNo)
                        {
                            device = info;
                            break;
                        }
                    
                    if (device == null)
                    {
                        device = new PseudoDevice(DeviceManager);
                        device.Address = 0;
                    }

                    device.DeviceId = dr.GetInt32(0);
                    device.SerialNo = dr.GetString(1);

                    if (dr.IsDBNull(2))
                        device.SiteId = null;
                    else
                        device.SiteId = dr.GetString(2);
                    */
                    bool useEnergyTotal = UseEnergyTotal(device.DeviceId.Value, day);

                    ResetInverterDay(device);

                    /* DMF
                    if (device.HasStartOfDayEnergyDefect && device.CrazyDayStartMinutes > 0.0)
                    {
                        // if defect present use deltas if drop found otherwise do not start with deltas
                        bool? energyDropFound = CheckForEnergyDrop(device, day, useEnergyTotal);
                        if (energyDropFound.HasValue)
                        {
                            device.StartEnergyResolved = true;
                            device.EnergyDropFound = energyDropFound.Value;
                        }
                    }
                    else
                    {
                        // inverters without defect do not start with deltas
                        device.StartEnergyResolved = true;
                        device.EnergyDropFound = false;
                    }
                    */
                    UpdateOneOutputHistory(device, day, device.UseEnergyTotal,
                        device.EnergyDropFound || !device.StartEnergyResolved || device.UseEnergyTotal);

                    ResetInverterDay(device);
                }

                stage = "end loop";
                dr.Close();
                dr.Dispose();
                dr = null;
            }
            catch (Exception e)
            {
                GlobalSettings.SystemServices.LogMessage("UpdateAllOutputHistory", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (con != null)
                {
                    con.Close();
                    con.Dispose();
                }

                if (haveMutex)
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
        }

        public DateTime FindNewStartDate(int imid)
        {
            List<DateTime> dateList;

            try
            {
                dateList = FindIncompleteDays(imid, false);
            }
            catch (Exception e)
            {
                throw new Exception("FindNewStartDate: " + e.Message, e);
            }

            DateTime newStartDate;

            if (dateList.Count > 0)
                newStartDate = dateList[0];
            else
                newStartDate = DateTime.Today.Date;

            return newStartDate;
        }

        public void UpdateNextFileDate(int imid, DateTime nextFileDate) 
        { 
            GenConnection connection = null; 
            Int32 result = -1; 
            GenCommand cmd = null; 
            string updCmd = 
                "update invertermanager set NextFileDate = @NextFileDate " + 
                "where Id = @InverterManagerId;"; 
            
            try 
            { 
                connection = GlobalSettings.TheDB.NewConnection(); 
                cmd = new GenCommand(updCmd, connection); 
                cmd.AddParameterWithValue("@NextFileDate", nextFileDate); 
                cmd.AddParameterWithValue("@InverterManagerId", imid); 
                result = cmd.ExecuteNonQuery(); 
            } 
            catch (System.Threading.ThreadInterruptedException e) { throw e; } 
            catch (Exception e) 
            { 
                throw new Exception("UpdateNextFileDate: " + e.Message, e); 
            } 
            finally 
            { 
                if (cmd != null)                     
                    cmd.Dispose(); 
                if (connection != null) 
                { 
                    connection.Close(); 
                    connection.Dispose(); 
                } 
            } 
        } 

        public DateTime FindNewInverterStartDate(IDevice device)
        {
            List<DateTime> dateList;

            try
            {
                dateList = FindInverterIncompleteDays(device, true);
            }
            catch (Exception e)
            {
                throw new Exception("FindNewInverterStartDate: " + e.Message, e);
            }

            DateTime newStartDate;

            if (dateList.Count > 0)
                newStartDate = dateList[0];
            else
                newStartDate = DateTime.Today.Date;

            return newStartDate;
        }

        public void UpdateNextInverterFileDate(IDevice device, DateTime nextFileDate)
        {
            GenConnection connection = null;
            Int32 result = -1;
            GenCommand cmd = null;

            string updCmd =
                "update inverter set NextFileDate = @NextFileDate " +
                "where Id = @InverterId;";

            try
            {
                connection = GlobalSettings.TheDB.NewConnection();
                cmd = new GenCommand(updCmd, connection);
                cmd.AddParameterWithValue("@NextFileDate", nextFileDate);
                cmd.AddParameterWithValue("@InverterId", device.DeviceId.Value);

                result = cmd.ExecuteNonQuery();
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new Exception("UpdateNextInverterFileDate: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
                if (connection != null)
                {
                    connection.Close();
                    connection.Dispose();
                }
            }
        }

        public void UpdatePastDates()
        {
            /*
            List<DateTime> dateList = FindIncompleteDays();

            foreach (DateTime date in dateList)
                if (date < DateTime.Today)
                    UpdateAllOutputHistory(date);

            DateTime nextDate = FindNewStartDate();

            if (NextFileDate == null || NextFileDate != nextDate)
            {
                bool haveMutex = false;
                try
                {
                    GlobalSettings.SystemServices.GetDatabaseMutex();
                    haveMutex = true;
                    UpdateNextFileDate(nextDate);
                    NextFileDate = nextDate;
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    if (haveMutex)
                        GlobalSettings.SystemServices.ReleaseDatabaseMutex();
                }
            }
            */
        }

        private DeviceStatus LocateInverterStatus(int id, String siteId)
        {
            foreach (DeviceStatus status in InverterStatusList)
            {
                if (status.Id == id)
                {
                    status.SiteId = siteId;
                    return status;
                }
            }

            DeviceStatus newStatus = new DeviceStatus(id, siteId, IntervalSeconds);
            InverterStatusList.Add(newStatus);
            return newStatus;
        }


        public int GetDeviceId(String make, String model, String serialNo, GenConnection connection, bool autoInsert, out String siteId)
        {
            GenCommand cmd = null;
            GenDataReader dr = null;

            string selCmd =
                "select i.Id, i.SiteId " +
                "from invertertype it, inverter i " +
                "where it.Id = i.inverterType_Id " +
                "and i.SerialNumber = @SerialNo " +
                "and it.Model = @Model " +
                "and it.Manufacturer = @Make;";
            try
            {
                cmd = new GenCommand(selCmd, connection);
                cmd.AddParameterWithValue("@SerialNo", serialNo);
                cmd.AddParameterWithValue("@Model", model);
                cmd.AddParameterWithValue("@Make", make);

                dr = (GenDataReader)cmd.ExecuteReader();

                if (dr.HasRows)
                {
                    if (dr.Read())
                    {
                        int i = dr.GetInt32(0);
                        String sid = null;

                        if (dr.IsDBNull(1))
                        {
                            if (GlobalSettings.ApplicationSettings.PvOutputSiteList.Count == 1)
                                sid = GlobalSettings.ApplicationSettings.PvOutputSiteList[0].SiteId;
                            else
                                sid = null;
                        }
                        else
                        {
                            sid = dr.GetString(1).Trim();
                        }

                        dr.Close();

                        siteId = sid;
                        LocateInverterStatus(i, sid);
                        return i;
                    }
                }
                else if (autoInsert)
                {
                    dr.Close();

                    if (GlobalSettings.ApplicationSettings.PvOutputSiteList.Count == 1)
                        siteId = GlobalSettings.ApplicationSettings.PvOutputSiteList[0].SiteId;
                    else
                        siteId = null;

                    int id = InsertDevice(make, model, serialNo, connection);
                    LocateInverterStatus(id, siteId);
                    return id;
                }

                dr.Close();

                throw new GenException(GenExceptionType.NoRowsReturned, "GetInverterId");
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.NoRowsReturned)
                    throw e;
                throw new Exception("GetInverterId - DB Error reading an inverter: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("GetInverterId - Error reading an inverter: " + e.Message, e);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();
                if (cmd != null)
                    cmd.Dispose();
            }
        }

        private int GetInverterTypeId(String make, String model, GenConnection connection, bool autoInsert)
        {
            GenCommand cmd = null;
            GenDataReader dr = null;

            string selCmd =
                "select it.Id " +
                "from invertertype it " +
                "where it.Model = @Model " +
                "and it.Manufacturer = @Make;";

            try
            {
                cmd = new GenCommand(selCmd, connection);
                cmd.AddParameterWithValue("@Model", model);
                cmd.AddParameterWithValue("@Make", make);

                dr = (GenDataReader)cmd.ExecuteReader();
                if (dr.HasRows)
                {
                    if (dr.Read())
                    {
                        int i = dr.GetInt32(0);
                        dr.Close();
                        return i;
                    }
                }
                else if (autoInsert)
                {
                    dr.Close();
                    return InsertInverterType(make, model, connection);
                }

                dr.Close();
                throw new GenException(GenExceptionType.NoRowsReturned, "GetInverterTypeId: No rows returned");
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.UniqueConstraintRowExists)
                    return 0;       // failure due to key conflict - row already exists

                throw new Exception("GetInverterTypeId - DB Error reading an inverter type: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("GetInverterTypeId - Error reading an inverter type: " + e.Message, e);
            }
            finally
            {
                if (dr != null)
                    dr.Dispose();
                if (cmd != null)
                    cmd.Dispose();
            }
        }

        private int InsertInverterType(String make, String model, GenConnection connection)
        {
            Int32 result = -1;
            GenCommand cmd = null;

            string insCmd =
                "insert into invertertype (Manufacturer, Model) " +
                "values ( @Make, @Model )";

            try
            {
                cmd = new GenCommand(insCmd, connection);
                cmd.AddParameterWithValue("@Make", make);
                cmd.AddParameterWithValue("@Model", model);

                result = cmd.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.UniqueConstraintRowExists)
                    return 0;       // failure due to key conflict - row already exists

                throw new Exception("InsertInverterType - DB Error inserting an inverter type: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("InsertInverterType - Error inserting an inverter type: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }


            //retrieve the new system generated InverterType_Id
            return GetInverterTypeId(make, model, connection, false);
        }

        public int InsertDevice(String make, String model, String serialNo, GenConnection connection)
        {
            Int32 inverterTypeId = GetInverterTypeId(make, model, connection, true);

            Int32 result = -1;
            GenCommand cmd = null;

            string insCmd =
                "insert into inverter (InverterType_Id, SerialNumber, SiteId) " +
                "values ( @InverterTypeId, @SerialNo, @SiteId) ";
            try
            {
                cmd = new GenCommand(insCmd, connection);
                cmd.AddParameterWithValue("@InverterTypeId", inverterTypeId);
                cmd.AddParameterWithValue("@SerialNo", serialNo);

                if (GlobalSettings.ApplicationSettings.PvOutputSiteList.Count == 1)
                    cmd.AddParameterWithValue("@SiteId", GlobalSettings.ApplicationSettings.PvOutputSiteList[0].SiteId);
                else
                    cmd.AddParameterWithValue("@SiteId", DBNull.Value);

                result = cmd.ExecuteNonQuery();
            }
            catch (GenException e)
            {
                if (e.Type == GenExceptionType.UniqueConstraintRowExists)
                    return 0;       // failure due to key conflict - row already exists

                throw new Exception("InsertInverter - DB Error inserting an inverter: " + e.Message, e);
            }
            catch (Exception e)
            {
                throw new Exception("InsertInverter - Error inserting an inverter: " + e.Message, e);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }

            //retrieve the new system generated InverterType_Id
            String siteId;
            return GetDeviceId(make, model, serialNo, connection, false, out siteId);
        }

        private void UpdateOutputHistory(IDevice device, DateTime day, bool useEnergyTotal, bool useDeltaAtStart)
        {
            String stage = "start";
            bool haveMutex = false;

            GlobalSettings.LogMessage("UpdateOutputHistory", "Day: " + day, LogEntryType.Trace);

            try
            {
                GlobalSettings.SystemServices.GetDatabaseMutex();
                haveMutex = true;
                stage = "new connection";

                UpdateOneOutputHistory(device, day, useEnergyTotal, useDeltaAtStart);
            }
            catch (Exception e)
            {
                GlobalSettings.LogMessage("UpdateOutputHistory", "Stage: " + stage + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (haveMutex)
                    GlobalSettings.SystemServices.ReleaseDatabaseMutex();
            }
        }

        public void UpdateOutputHistoryInterval()
        {
            //state = "before UpdateOutputHistory";
            int newInterval = ((int)DateTime.Now.TimeOfDay.TotalSeconds) / IntervalSeconds;
            if (GlobalSettings.SystemServices.LogTrace)
                GlobalSettings.LogMessage("UpdateOutputHistoryInterval", "NewInterval: " + newInterval +
                    " - PrevInterval: " + PrevInterval, LogEntryType.Trace);
            if (newInterval == PrevInterval)
                return;
            /* DMF
            foreach (IDevice iInfo in DeviceManager.DeviceList)
            {
                if (iInfo.Found && iInfo.DeviceId != null)
                {
                    // If energy today is present energy total is not required for current day
                    iInfo.UseEnergyTotal = UseEnergyTotal(iInfo.DeviceId.Value, ProcessingDay);

                    if (!iInfo.StartEnergyResolved)
                        if (iInfo.HasStartOfDayEnergyDefect && iInfo.CrazyDayStartMinutes > 0.0)
                        {
                            // if defect present use deltas if drop found otherwise do not start with deltas
                            bool? energyDropFound = CheckForEnergyDrop(iInfo, ProcessingDay, iInfo.UseEnergyTotal);
                            if (energyDropFound.HasValue)
                            {
                                iInfo.StartEnergyResolved = true;
                                iInfo.EnergyDropFound = energyDropFound.Value;
                            }
                        }
                        else
                        {
                            // inverters without defect do not start with deltas
                            iInfo.StartEnergyResolved = true;
                            iInfo.EnergyDropFound = false;
                        }
                    UpdateOutputHistory(iInfo, ProcessingDay, iInfo.UseEnergyTotal,
                        iInfo.EnergyDropFound || !iInfo.StartEnergyResolved || iInfo.UseEnergyTotal);
                }
            }
            */
            PrevInterval = newInterval;
        }
    }
}


