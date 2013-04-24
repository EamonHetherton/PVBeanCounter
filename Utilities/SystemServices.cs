/*
* Copyright (c) 2010 Dennis Mackay-Fisher
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
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using Mischel.Synchronization;
using NetworkLib;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

namespace MackayFisher.Utilities
{
    public enum LogEntryType
    {
        StatusChange,
        ErrorMessage,
        Information,
        Format,
        Trace,
        MeterTrace,
        MeterMessage,
        Database,
        Event
    }

    [Flags]
    public enum EXECUTION_STATE : uint
    {
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001,
        ES_AWAYMODE_REQUIRED = 0x00000040
    }

    public enum SuspendPowerState
    {
        Suspend = 0,
        Hibernate = 1,
        Idle = 2
    }

    public interface IUtilityLog
    {
        bool LogStatus { get; }
        bool LogFormat { get; }
        bool LogInformation { get; }
        bool LogError { get; }
        bool LogTrace { get; }
        bool LogMeterTrace { get; }
        bool LogMessageContent { get; }
        bool LogDatabase { get; }
        bool LogEvent { get; }

        void LogMessage(string component, string message, LogEntryType logEntryType);

        void GetDatabaseMutex();
        void ReleaseDatabaseMutex();
    }

    public class TimerSettings
    {
        public string Id;
        public Mischel.Synchronization.WaitableTimer WakeTimer = null;
        public DateTime? WakeUpDateTime = null;
        public DateTime? NextWakeUpDateTime = null;
        public Mutex TimerMutex = new Mutex();
        public System.Threading.Thread WakeTimerThread = null;
    }

    public class SystemServices :IUtilityLog
    {
        // TimerSettings1 is for the main timer that resumes from a suspend state
        private TimerSettings TimerSettings1 = null;
        // TimerSettings2 is for the backup timer - in case a race condition allows 
        // a suspend to occur before a new TimerSettings1 timer is activated
        private TimerSettings TimerSettings2 = null;

        bool logError = true;
        bool logFormat = true;
        bool logStatus = true;
        bool logInformation = true;
        bool logTrace = false;
        bool logMeterTrace = false;
        bool logMessageContent = false;
        bool logDatabase = false;
        bool logEvent = false;

        private static Mutex DatabaseMutex = new Mutex();
        private static Mutex LogMutex = new Mutex();

        public bool UseDatabaseMutex { get; set; }

        private bool errorMode = false;

        public void GetDatabaseMutex()
        {
            if (UseDatabaseMutex)
            {
                if (logDatabase)
                    LogMessage("SystemServices.GetDatabaseMutex", "Thread: " + 
                        System.Threading.Thread.CurrentThread.ManagedThreadId + " - wait mutex", LogEntryType.Database);
                DatabaseMutex.WaitOne();
                if (logDatabase)
                    LogMessage("SystemServices.GetDatabaseMutex", "Thread: " +
                        System.Threading.Thread.CurrentThread.ManagedThreadId + " - mutex acquired", LogEntryType.Database);
            }
        }

        public void ReleaseDatabaseMutex()
        {
            if (UseDatabaseMutex)
            {
                DatabaseMutex.ReleaseMutex();
                if (logDatabase)
                    LogMessage("SystemServices.GetDatabaseMutex", "Thread: " +
                        System.Threading.Thread.CurrentThread.ManagedThreadId + " - mutex released", LogEntryType.Database);
            }
        }

        public bool LogError
        {
            get
            {
                return logError;
            }
            set
            {
                logError = value;
            }
        }

        public bool LogFormat
        {
            get
            {
                return logFormat;
            }
            set
            {
                logFormat = value;
            }
        }

        public bool LogStatus
        {
            get
            {
                return logStatus;
            }
            set
            {
                logStatus = value;
            }
        }

        public bool LogInformation
        {
            get
            {
                return logInformation;
            }
            set
            {
                logInformation = value;
            }
        }

        public bool LogTrace
        {
            get
            {
                return logTrace;
            }
            set
            {
                logTrace = value;
            }
        }

        public bool LogMeterTrace
        {
            get
            {
                return logMeterTrace;
            }
            set
            {
                logMeterTrace = value;
            }
        }

        public bool LogMessageContent
        {
            get
            {
                return logMessageContent;
            }
            set
            {
                logMessageContent = value;
            }
        }

        public bool LogDatabase
        {
            get
            {
                return logDatabase;
            }
            set
            {
                logDatabase = value;
            }
        }

        public bool LogEvent
        {
            get
            {
                return logEvent;
            }
            set
            {
                logEvent = value;
            }
        }

        public int ErrorLogCount { get; set; }
        
        private String LogFileFullName;
        private String LogFileName;
        private System.IO.TextWriter LogFile;

        [DllImport("Kernel32.DLL", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE state);

        public static void SetThreadExecutionState( bool continuous, bool displayRequired, bool systemRequired, bool awaymodeRequired)
        {
            EXECUTION_STATE es = 0;

            if (continuous)
                es = EXECUTION_STATE.ES_CONTINUOUS;
            if (systemRequired)
                es |= EXECUTION_STATE.ES_SYSTEM_REQUIRED;
            if (awaymodeRequired)
                es |= EXECUTION_STATE.ES_AWAYMODE_REQUIRED;

            //SetThreadExecutionState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
            SetThreadExecutionState(es);
            // the example I followed used a separate call for display required
            if (displayRequired)
                SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED);
        }

        public static String BytesToHex(ref byte[] input, int groupSize = 1, String separator = "", String prefix = "0x", long start = 0, long? length = null)
        {
            String output = prefix;
            String hexOutput;
            long outLength = 0;
            int cnt = groupSize;

            for (long i = start; i < input.Length; i++)
            {
                if (length.HasValue && length == outLength)
                    break;
                outLength++;
                int value = Convert.ToInt32(input[i]);
                // Convert the decimal value to a hexadecimal value in string form.
                hexOutput = String.Format("{0:X2}", value);
                if (cnt > 0)
                    output = output + hexOutput;                    
                else
                {
                    output = output + separator + hexOutput;
                    cnt = groupSize;
                }
                cnt--;
            }
            return output;
        }

        public static byte[] HexToBytes(String hexString)
        {
            String str;
            if (hexString.StartsWith("0x"))
                str = hexString.Substring(2);
            else
                str = hexString;

            str = str.Replace(" ", "");
            int numBytes = (str.Length + 1) / 2;

            byte[] bytes = new byte[numBytes];

            for (int i = 0; i < numBytes; i++)
            {
                bytes[i] = byte.Parse(str.Substring(i * 2, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            }

            return bytes;
        }

        public static Decimal HexToDecimal(String hexString)
        {
            byte[] bytes = HexToBytes(hexString);
            Decimal val = 0;
            for (int i = 0; i < bytes.Length; i++)
                val = val * 256 + bytes[i];

            return val;
        }

        public static UInt16 HexToUInt16(String hexString)
        {
            byte[] bytes = HexToBytes(hexString);
            UInt16 val = 0;
            for (int i = 0; i < bytes.Length; i++)
                val = (UInt16)(val * 256 + bytes[i]);

            return val;
        }

        public static UInt32 HexToUInt32(String hexString)
        {
            byte[] bytes = HexToBytes(hexString);
            UInt32 val = 0;
            for (int i = 0; i < bytes.Length; i++)
                val = (val * 256 + bytes[i]);

            return val;
        }

        public static UInt64 HexToUInt64(String hexString)
        {
            byte[] bytes = HexToBytes(hexString);
            UInt64 val = 0;
            for (int i = 0; i < bytes.Length; i++)
                val = (val * 256 + bytes[i]);

            return val;
        }

        public static byte[] StringToBytes(String input)
        {
            char[] values = input.ToCharArray();
            byte[] output = new byte[values.Length];
            int i;
            for (i = 0; i < values.Length; i++)
                output[i] = (byte)values[i];

            return output;
        }

        public static String BytesToString(ref byte[] input, int size, int start = 0, bool cStringNull = false)
        {
            int outSize = size;
            if (start + outSize > input.Length)
                outSize = input.Length - start;
            if (outSize == 0)
                return "";
            if (outSize < 0)
                // return "";
                // Need to know if this is occuring
                throw new Exception("BytesToString size error - Size: " + size + " - Start: " + start);

            char[] output = new char[outSize];
            int inPos = start;
            int strSize = outSize;
            bool nullFound = false;
            for (int i = 0; i < outSize; i++)
            {
                byte b = input[inPos++];
                if (cStringNull && b == 0 && !nullFound)
                {
                    strSize = inPos - 1;
                    nullFound = true;
                }
                output[i] = (char)b;
            }

            StringBuilder sb = new StringBuilder(output.Length);
            sb.Append(output, 0, strSize);

            return sb.ToString();
        }

        public SystemServices()
        {
            LogFile = null;
            UseDatabaseMutex = false;
            ErrorLogCount = 0;
        }

        public SystemServices(String logFileName)
        {
            LogFile = null;
            OpenLogFile(logFileName);
            UseDatabaseMutex = false;
        }

        public void ManageOldLogFiles(String defaultDirectory, String archiveDirectory, int? retainDays)
        {
            DirectoryInfo directoryInInfo = new DirectoryInfo(defaultDirectory);

            if (!directoryInInfo.Exists)
                throw new Exception("ManageOldLogFiles - Directory: " + defaultDirectory + " :does not exist");

            DirectoryInfo directoryMoveToInfo = new DirectoryInfo(archiveDirectory);
            if (!directoryMoveToInfo.Exists)
                try
                {
                    directoryMoveToInfo.Create();
                }
                catch (Exception e)
                {
                    throw new Exception("ManageOldLogFiles - Error creating directory: " + archiveDirectory + " - Exception: " + e.Message, e);
                }

            foreach (FileInfo fileInfo in directoryInInfo.EnumerateFiles("PVService_????????.log"))
            {
                if (fileInfo.Name != LogFileName)
                    MoveFileToArchive(archiveDirectory, directoryMoveToInfo, fileInfo);
            }

            foreach (FileInfo fileInfo in directoryInInfo.EnumerateFiles("PVService.log"))
            {
                if (fileInfo.Name != LogFileName)
                    MoveFileToArchive(archiveDirectory, directoryMoveToInfo, fileInfo);
            }

            if (retainDays.HasValue)
            {
                foreach (FileInfo fileInfo in directoryMoveToInfo.EnumerateFiles("PVService_????????.log"))
                {
                    bool isOld = false;

                    String fileDateStr = fileInfo.Name.Substring(fileInfo.Name.Length - 12, 8);
                    DateTime fileDate = DateTime.ParseExact(fileDateStr, "yyyyMMdd", CultureInfo.InvariantCulture);

                    isOld = (DateTime.Today - fileDate).TotalDays > retainDays.Value;

                    if (isOld)
                    {
                        try
                        {
                            fileInfo.Delete();
                        }
                        catch (Exception e)
                        {
                            throw new Exception("ManageOldLogFiles - Error deleting file: " + fileInfo.FullName + " - Exception: " + e.Message, e);
                        }
                    }
                }
            }
        }

        private static void MoveFileToArchive(String archiveDirectory, DirectoryInfo directoryMoveToInfo, FileInfo fileInfo)
        {
            try
            {
                String newName = System.IO.Path.Combine(directoryMoveToInfo.FullName, fileInfo.Name);
                FileInfo moveToFileInfo = new FileInfo(newName);
                if (moveToFileInfo.Exists)
                    moveToFileInfo.Delete();
                moveToFileInfo = null;

                fileInfo.MoveTo(newName);
            }
            catch (Exception e)
            {
                throw new Exception("MoveFileToArchive - Error moving file: " + fileInfo.FullName + " :to: " + archiveDirectory + " - Exception: " + e.Message, e);
            }
        }

        public void OpenLogFile(String logFileName)
        {
            LogMutex.WaitOne();

            if (LogFile != null)
                CloseLogFile();

            LogFileFullName = logFileName;

            System.IO.FileInfo fileInfo = new System.IO.FileInfo(LogFileFullName);
            if (fileInfo.Exists)
                LogFile = fileInfo.AppendText();
            else
                LogFile = fileInfo.CreateText();

            LogFileName = fileInfo.Name;
            if (errorMode)
            {
                // force error header at log start
                errorMode = false;
                CheckErrorMode(true);
            }
            LogMutex.ReleaseMutex();
        }

        public void CloseLogFile()
        {
            if (LogFile != null)
            {
                LogFile.Close();
                LogFile = null;
            }
        }

        private void CheckErrorMode(bool errorFound)
        {
            if (errorFound && !errorMode)
            {
                LogFile.WriteLine(">>>>>> Errors Found");
                errorMode = true;
            }
            else if (!errorFound && errorMode)
            {
                LogFile.WriteLine("<<<<<< End Errors");
                errorMode = false;
            }            
        }

        public void LogMessage(String component, String message, LogEntryType logEntryType)
        {
            if (logEntryType == LogEntryType.StatusChange && !LogStatus)
                return;
            if (logEntryType == LogEntryType.Information && !LogInformation)
                return;
            if (logEntryType == LogEntryType.ErrorMessage && !LogError)
                return;
            if (logEntryType == LogEntryType.Format && !LogFormat)
                return;
            if (logEntryType == LogEntryType.Trace && !LogTrace)
                return;
            if (logEntryType == LogEntryType.MeterTrace && !LogMeterTrace)
                return;
            if (logEntryType == LogEntryType.MeterMessage && !LogMessageContent)
                return;
            if (logEntryType == LogEntryType.Database && !LogDatabase)
                return;
            if (logEntryType == LogEntryType.Event && !LogEvent)
                return;

            CheckErrorMode(logEntryType == LogEntryType.ErrorMessage);

            int id = Thread.CurrentThread.ManagedThreadId;
            String threadName = "";

            if (Thread.CurrentThread.Name != null)
                threadName = Thread.CurrentThread.Name;

            if (logEntryType == LogEntryType.ErrorMessage)
                ErrorLogCount++;

            // The mutex avoids collisions between messages from concurrent threads
            LogMutex.WaitOne();
            if (logEntryType == LogEntryType.Format)
                LogFile.WriteLine(message);
            else if (threadName == "")
                LogFile.WriteLine(DateTime.Now + " :T" + id.ToString("00") + " :" + component + ": " + message);
            else
                LogFile.WriteLine(DateTime.Now + " :T" + id.ToString("00") + " " + threadName + " :" + component + ": " + message);

            LogFile.Flush();
            LogMutex.ReleaseMutex();
        }
      
        private void KillWakeTimerThread(TimerSettings settings)
        {
            if (settings == null)
                return;

            settings.TimerMutex.WaitOne();

            if (settings.WakeTimerThread != null)
            {
                if (settings.WakeTimerThread.IsAlive)
                {
                    try
                    {
                        LogMessage("SystemServices", "KillWakeTimerThread - id " + settings.Id, LogEntryType.Trace);
                        settings.WakeTimerThread.Abort();
                        if (settings.WakeTimer != null)
                        {
                            settings.WakeTimer.Cancel();
                            settings.WakeTimer.Close();
                            settings.WakeTimer.Dispose();
                            settings.WakeTimer = null;
                        }
                        // allow dust to settle!!!
                        System.Threading.Thread.Sleep(1000);
                    }
                    catch (Exception e)
                    {
                        LogMessage("SystemServices", "KillWakeTimerThread - id " + settings.Id + " - Exception: " + e.Message, LogEntryType.ErrorMessage);
                    }
                }
                settings.WakeTimerThread = null;
            }
            settings.TimerMutex.ReleaseMutex();
        }

        public void KillWakeTimers()
        {
            // used with a full shutdown so that the computer does not wake from suspend
            // and to ensure that the timer threads end quickly
            KillWakeTimerThread(TimerSettings1);
            KillWakeTimerThread(TimerSettings2);
        }
        
        private bool ChangeTimer(TimerSettings timerSettings)
        {
            try{
                    int rslt;

                    if (timerSettings.WakeTimer == null)
                        timerSettings.WakeTimer = new Mischel.Synchronization.WaitableTimer(true);

                    if (timerSettings.WakeUpDateTime == null)
                    {
                        LogMessage("SystemServices", "ChangeTimer - Timer " + timerSettings.Id + " null wake time", LogEntryType.Trace);
                        // after 10 seconds timer thread will expire
                        TimeSpan t = TimeSpan.FromSeconds(10);
                        rslt = timerSettings.WakeTimer.Change(t, 0, null, null, false);
                    }
                    else
                    {
                        LogMessage("SystemServices", "ChangeTimer - Timer " + timerSettings.Id + " set - wake at: " + timerSettings.WakeUpDateTime, LogEntryType.Trace);
                        TimeSpan t = timerSettings.WakeUpDateTime.Value - DateTime.Now;
                        if (t.TotalSeconds < 0)
                        {
                            LogMessage("SystemServices", "ChangeTimer - attempt to set negative timer " 
                                + timerSettings.Id + " - Wake Time: " + timerSettings.WakeUpDateTime.Value, LogEntryType.ErrorMessage);
                            t = TimeSpan.FromSeconds(10);
                        }
                        rslt = timerSettings.WakeTimer.Change(t, 0, null, null, true);
                    }

                    if (rslt == Mischel.Synchronization.WaitableTimer.ErrorNotSupported)
                    {
                        LogMessage("SystemServices", "ChangeTimer - System does not support resume", LogEntryType.ErrorMessage);
                        timerSettings.WakeUpDateTime = null;
                        return false;
                    }
                    
                    return true;
                }
                catch (Exception e)
                {
                    LogMessage("SystemServices", "ChangeTimer - Timer " + timerSettings.Id + " - Exception - Wake Time: " + timerSettings.WakeUpDateTime.Value
                        + " - Exception: " + e.Message, LogEntryType.Trace);
                    return false;
                }
        }

        private void RunWakeTimer(Object timerSettingsObj)
        {
            TimerSettings timerSettings = (TimerSettings)timerSettingsObj;
            timerSettings.TimerMutex.WaitOne();
            bool haveMutex = true;

            while (timerSettings.WakeUpDateTime != null)
            {
                try
                {
                    if (ChangeTimer(timerSettings))
                    {                   
                        timerSettings.TimerMutex.ReleaseMutex();
                        haveMutex = false;

                        timerSettings.WakeTimer.WaitOne();
                        System.Threading.Thread.Sleep(3000);

                        timerSettings.TimerMutex.WaitOne();
                        haveMutex = true;
                        timerSettings.WakeUpDateTime = timerSettings.NextWakeUpDateTime;
                        timerSettings.NextWakeUpDateTime = null;
                                             
                    }
                }
                catch (Exception e)
                {
                    LogMessage("SystemServices", "RunWakeTimer - Exception (expected) - Wake Time: " + timerSettings.WakeUpDateTime.Value
                        + " - Exception: " + e.Message, LogEntryType.Trace);
                }
                finally
                {
                    LogMessage("SystemServices", "RunWakeTimer - Timer expired", LogEntryType.Trace);
                }
            }

            timerSettings.WakeTimerThread = null;
            if (haveMutex)
                timerSettings.TimerMutex.ReleaseMutex();   
        }

        // sets the wakeup timers and suspends the computer
        public void SetupSuspendForDuration(SuspendPowerState powerState, DateTime wakeTime, DateTime? nextWakeTime)
        {
            LogMessage("SystemServices", "SetupSuspendForDuration: " + wakeTime, LogEntryType.Trace);

            SetupWakeEvent(wakeTime, nextWakeTime);

            if (powerState == SuspendPowerState.Idle)
                return;

            // Allow a little time to ensure timer is ready
            Thread.Sleep(1000);

            RestartOptions ro = RestartOptions.Suspend;

            if (powerState == SuspendPowerState.Hibernate)
                ro = RestartOptions.Hibernate;
            WindowsController.ExitWindows(ro, false);
        }

        // sets the primary and backup timers. These timers are used solely to wake the computer
        // from a suspended state. No operational functionality is associated with these timers
        public void SetupWakeEvent(DateTime? wakeTime, DateTime? nextWakeTime)
        {
            // LogMessage("SystemServices", "SetupWakeEvent - Timer 1: " + wakeTime + " - Timer 2: " + nextWakeTime, LogEntryType.Trace);
            bool haveMutex1 = false;
            bool haveMutex2 = false;

            try
            {

                if (TimerSettings1 == null)
                {
                    TimerSettings1 = new TimerSettings();
                    TimerSettings1.Id = "1";
                }

                TimerSettings1.TimerMutex.WaitOne();
                haveMutex1 = true;

                TimerSettings1.NextWakeUpDateTime = nextWakeTime;

                if (wakeTime == TimerSettings1.WakeUpDateTime)
                {
                    // LogMessage("SystemServices", "SetupWakeEvent: Timer 1 already set: " + wakeTime, LogEntryType.Trace);
                }
                else
                {
                    // timer1 is the primary timer. This will normally be the only timer 
                    // that actually triggers. 
                    TimerSettings1.WakeUpDateTime = wakeTime;

                    if (TimerSettings1.WakeTimerThread == null)
                    {
                        TimerSettings1.WakeTimerThread = new System.Threading.Thread(RunWakeTimer);
                        TimerSettings1.WakeTimerThread.Start(TimerSettings1);
                    }
                    else
                        ChangeTimer(TimerSettings1);
                }

                TimerSettings1.TimerMutex.ReleaseMutex();
                haveMutex1 = false;


                if (TimerSettings2 == null)
                {
                    TimerSettings2 = new TimerSettings();
                    TimerSettings2.Id = "2";
                }

                TimerSettings2.TimerMutex.WaitOne();
                haveMutex2 = true;

                if (nextWakeTime == TimerSettings2.WakeUpDateTime)
                {
                    // LogMessage("SystemServices", "SetupWakeEvent: Timer 2 already set: " + nextWakeTime, LogEntryType.Trace);
                }
                else
                {
                    // timer 2 is a backup timer. In the event that the computer is suspended
                    // after the primary timer has fired but before the timer has been reset
                    // the secondary timer will eventually wake the computer
                    TimerSettings2.WakeUpDateTime = nextWakeTime;

                    if (TimerSettings2.WakeTimerThread == null)
                    {
                        TimerSettings2.WakeTimerThread = new System.Threading.Thread(RunWakeTimer);
                        TimerSettings2.WakeTimerThread.Start(TimerSettings2);
                    }
                    else
                        ChangeTimer(TimerSettings2);
                }

                TimerSettings2.TimerMutex.ReleaseMutex();
                haveMutex2 = false;
            }
            catch (Exception e)
            {
                LogMessage("SystemServices", "SetupWakeEvent - Exception: " + e.Message, LogEntryType.ErrorMessage);
            }
            finally
            {
                if (haveMutex1)
                    TimerSettings1.TimerMutex.ReleaseMutex();
                if (haveMutex2)
                    TimerSettings2.TimerMutex.ReleaseMutex();
            }

        }

    }
}
