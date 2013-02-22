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
using System.Diagnostics;
using System.Linq;
using System.Text;
using PVSettings;
using MackayFisher.Utilities;
using PVBCInterfaces;

namespace PVInverterManagement
{
    public class SunnyExplorerExtractCSV : ExtractCSV
    {
        private const int MaxSunnyExplorerRunTime = 3; // minutes

        private InverterManagerSettings InverterManagerSettings;

        private static StringBuilder Output = null;
        private static StringBuilder ErrorOutput = null;

        public override string ComponentType
        {
            get { return "SunnyExplorerExtractCSV"; }
        }

        public SunnyExplorerExtractCSV(SunnyExplorerInverterManager inverterManager, InverterManagerSettings imSettings):
            base(inverterManager)
        {
            InverterManagerSettings = imSettings;
        }

        public override bool RunExtract(String configFile, String password, String exportDirectory, DateTime fromDate, DateTime toDate)
        {
            String executablePath = "";
            String args = "";
            Process exeProcess = null;
            String configFileLocal;
            String passwordLocal;

            if (configFile == null || configFile == "")
                configFileLocal = ApplicationSettings.BuildFileName("SunnyExplorer.sx2", exportDirectory);
            else
                configFileLocal = ApplicationSettings.BuildFileName(configFile, exportDirectory);

            if (password == null || password == "")
            {
                passwordLocal = InverterManagerSettings.SunnyExplorerPassword;
                if (passwordLocal == null || passwordLocal == "")
                    passwordLocal = "0000"; // default password for some SMA inverters
            }
            else
                passwordLocal = password;

            try
            {
                executablePath = InverterManagerSettings.SunnyExplorerPath;
                args = "\"" + configFileLocal + "\" -userlevel user -password \"" + passwordLocal + "\" -exportdir \""
                    + exportDirectory + "\" -exportrange \"" + fromDate.Date.ToString("yyyyMMdd") + "-" + toDate.Date.ToString("yyyyMMdd") + "\" "
                    + "-export energy5min";

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = executablePath;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.Arguments =args;

                Output = new StringBuilder("");
                ErrorOutput = new StringBuilder("");

                exeProcess = new Process();
                exeProcess.OutputDataReceived += OutputHandler;
                exeProcess.ErrorDataReceived += ErrorOutputHandler;

                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                exeProcess.StartInfo = startInfo;

                LogMessage("Starting SunnyExplorer: " + args, LogEntryType.Trace);
                //exeProcess = Process.Start(startInfo);
                exeProcess.Start();
                exeProcess.BeginOutputReadLine();
                exeProcess.BeginErrorReadLine();
                LogMessage("SunnyExplorer Started - waiting", LogEntryType.Trace);
                exeProcess.WaitForExit(MaxSunnyExplorerRunTime*60000);
                if (!exeProcess.HasExited)
                {
                    try
                    {
                        LogMessage("SunnyExplorer taking too long - over " + MaxSunnyExplorerRunTime + " minutes", LogEntryType.ErrorMessage);
                        exeProcess.Kill();
                        LogMessage("SunnyExplorer killed", LogEntryType.ErrorMessage);
                    }
                    catch (Exception e)
                    {
                        LogMessage("Exception killing SunnyExplorer: " + e.Message, LogEntryType.ErrorMessage);
                    }
                }
                else
                {
                    LogMessage("SunnyExplorer Completed: exit code = " + exeProcess.ExitCode, LogEntryType.Trace);
                }
            }
            catch (System.Threading.ThreadInterruptedException e)
            {
                LogMessage("ThreadInterruptedException", LogEntryType.Information);
                try
                {
                    if (!exeProcess.HasExited)
                        exeProcess.Kill();
                }
                catch (Exception)
                {
                }
                throw e;
            }
            catch(Exception e)
            {
                LogMessage("Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.ProcessFailed, "RunExtract: failure executing: " + executablePath + " " + args + " :" + e.Message, e);
            }

            try
            {
                String output = Output.ToString();
                if (output.Contains("CSV-Export: completed"))
                {
                    if (GlobalSettings.SystemServices.LogTrace)
                    {
                        LogMessage("Sunny Explorer - Standard Output: " + output, LogEntryType.Trace);
                        LogMessage("Sunny Explorer - Error Output: " + ErrorOutput.ToString(), LogEntryType.Trace);
                    }
                }
                else
                {
                    LogMessage("Sunny Explorer - CSV Export Failed" + output, LogEntryType.ErrorMessage);
                    LogMessage("Sunny Explorer - Standard Output: " + output, LogEntryType.ErrorMessage);
                    LogMessage("Sunny Explorer - Error Output: " + ErrorOutput.ToString(), LogEntryType.ErrorMessage);
                }
            }
            catch (Exception e)
            {
                LogMessage("RunExtract - Exception: " + e.Message, LogEntryType.ErrorMessage);
                throw new PVException(PVExceptionType.UnexpectedError, "RunExtract: failure examining SE output: " + e.Message, e);
            }  

            return true;
        }

        private static void OutputHandler(object sendingProcess,
            DataReceivedEventArgs outLine)
        {
            try
            {
                if (!String.IsNullOrEmpty(outLine.Data))
                {
                    // Add the text to the collected output.
                    Output.Append(Environment.NewLine + outLine.Data);
                }
            }
            catch (Exception e)
            {
                try
                {
                    Output.Append(Environment.NewLine + "OutputHandler - Exception: " + e.Message);
                }
                finally
                {
                }
            }
        }

        private static void ErrorOutputHandler(object sendingProcess,
            DataReceivedEventArgs outLine)
        {
            try
            {
                if (!String.IsNullOrEmpty(outLine.Data))
                {
                    // Add the text to the collected output.
                    ErrorOutput.Append(Environment.NewLine + outLine.Data);
                }
            }
            catch (Exception e)
            {
                try
                {
                    Output.Append(Environment.NewLine + "ErrorOutputHandler - Exception: " + e.Message);
                }
                finally
                {
                }
            }
        }

    }
}
